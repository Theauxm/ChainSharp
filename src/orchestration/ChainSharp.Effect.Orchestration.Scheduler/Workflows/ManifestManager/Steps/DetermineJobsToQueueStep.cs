using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager.Utilities;
using ChainSharp.Effect.Services.EffectStep;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager.Steps;

/// <summary>
/// Determines which manifests are due for execution based on their scheduling rules.
/// </summary>
internal class DetermineJobsToQueueStep(
    SchedulerConfiguration config,
    ILogger<DetermineJobsToQueueStep> logger
) : EffectStep<(List<Manifest>, List<DeadLetter>), List<Manifest>>
{
    public override async Task<List<Manifest>> Run((List<Manifest>, List<DeadLetter>) input)
    {
        var (manifests, newlyCreatedDeadLetters) = input;

        logger.LogDebug(
            "Starting DetermineJobsToQueueStep to identify manifests due for execution"
        );

        var now = DateTime.UtcNow;
        var manifestsToQueue = new List<Manifest>();

        // Create a set of manifest IDs that were just dead-lettered in this cycle
        var newlyDeadLetteredManifestIds = newlyCreatedDeadLetters
            .Select(dl => dl.ManifestId)
            .ToHashSet();

        // Check global active job limit (Pending + InProgress across all manifests)
        if (config.MaxActiveJobs.HasValue)
        {
            var totalActiveJobs = manifests
                .SelectMany(m => m.Metadatas)
                .Count(
                    m =>
                        m.WorkflowState == WorkflowState.Pending
                        || m.WorkflowState == WorkflowState.InProgress
                );

            if (totalActiveJobs >= config.MaxActiveJobs.Value)
            {
                logger.LogInformation(
                    "MaxActiveJobs limit reached ({TotalActiveJobs}/{MaxActiveJobs}). Skipping job enqueueing this cycle.",
                    totalActiveJobs,
                    config.MaxActiveJobs.Value
                );
                return manifestsToQueue;
            }

            // Calculate how many more jobs we can queue before hitting the global limit
            var remainingCapacity = config.MaxActiveJobs.Value - totalActiveJobs;
            logger.LogDebug(
                "Active job capacity: {TotalActiveJobs}/{MaxActiveJobs} ({RemainingCapacity} remaining)",
                totalActiveJobs,
                config.MaxActiveJobs.Value,
                remainingCapacity
            );
        }

        // Filter to only scheduled manifests (not manual-only)
        var scheduledManifests = manifests.Where(m => m.ScheduleType != ScheduleType.None).ToList();

        logger.LogDebug(
            "Found {ManifestCount} enabled scheduled manifests to evaluate",
            scheduledManifests.Count
        );

        foreach (var manifest in scheduledManifests)
        {
            // Skip if this manifest was just dead-lettered in this cycle
            if (newlyDeadLetteredManifestIds.Contains(manifest.Id))
            {
                logger.LogTrace(
                    "Skipping manifest {ManifestId} - was just dead-lettered in this cycle",
                    manifest.Id
                );
                continue;
            }

            // Skip if there's already an AwaitingIntervention dead letter using in-memory data
            var hasDeadLetter = manifest.DeadLetters.Any(
                dl => dl.Status == DeadLetterStatus.AwaitingIntervention
            );

            if (hasDeadLetter)
            {
                logger.LogTrace(
                    "Skipping manifest {ManifestId} - has AwaitingIntervention dead letter",
                    manifest.Id
                );
                continue;
            }

            // Skip if there's already a Pending or InProgress execution using in-memory data
            var hasActiveExecution = manifest.Metadatas.Any(
                m =>
                    m.WorkflowState == WorkflowState.Pending
                    || m.WorkflowState == WorkflowState.InProgress
            );

            if (hasActiveExecution)
            {
                logger.LogTrace(
                    "Skipping manifest {ManifestId} - has pending or in-progress execution",
                    manifest.Id
                );
                continue;
            }

            // Check if this manifest is due for execution
            if (SchedulingHelpers.ShouldRunNow(manifest, now, logger))
            {
                logger.LogDebug(
                    "Manifest {ManifestId} (name: {ManifestName}) is due for execution",
                    manifest.Id,
                    manifest.Name
                );
                manifestsToQueue.Add(manifest);

                // Check MaxActiveJobs limit (current active + jobs we're about to queue)
                if (config.MaxActiveJobs.HasValue)
                {
                    var currentActiveJobs = manifests
                        .SelectMany(m => m.Metadatas)
                        .Count(
                            m =>
                                m.WorkflowState == WorkflowState.Pending
                                || m.WorkflowState == WorkflowState.InProgress
                        );

                    if (currentActiveJobs + manifestsToQueue.Count >= config.MaxActiveJobs.Value)
                    {
                        logger.LogInformation(
                            "Reached MaxActiveJobs limit ({CurrentActive} active + {ToQueue} to queue >= {MaxActiveJobs}), will queue remaining manifests in next poll cycle",
                            currentActiveJobs,
                            manifestsToQueue.Count,
                            config.MaxActiveJobs.Value
                        );
                        break;
                    }
                }
            }
        }

        logger.LogInformation(
            "DetermineJobsToQueueStep completed: {ManifestsToQueueCount} manifests are due for execution",
            manifestsToQueue.Count
        );

        return manifestsToQueue;
    }
}
