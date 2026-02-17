using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager.Utilities;
using ChainSharp.Effect.Services.EffectStep;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager.Steps;

/// <summary>
/// Determines which manifests are due for execution based on their scheduling rules.
/// </summary>
/// <remarks>
/// MaxActiveJobs is NOT enforced here — that responsibility belongs to the JobDispatcher,
/// which is the single gateway to the BackgroundTaskServer. This step freely identifies
/// all manifests that are due, applying only per-manifest guards (dead letters, duplicate
/// queue entries, active executions).
/// </remarks>
internal class DetermineJobsToQueueStep(ILogger<DetermineJobsToQueueStep> logger)
    : EffectStep<(List<Manifest>, List<DeadLetter>), List<Manifest>>
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

        // Filter to only time-based scheduled manifests (not manual-only or dependent)
        var scheduledManifests = manifests
            .Where(
                m => m.ScheduleType != ScheduleType.None && m.ScheduleType != ScheduleType.Dependent
            )
            .ToList();

        logger.LogDebug(
            "Found {ManifestCount} enabled scheduled manifests to evaluate",
            scheduledManifests.Count
        );

        foreach (var manifest in scheduledManifests)
        {
            if (ShouldSkipManifest(manifest, newlyDeadLetteredManifestIds))
                continue;

            // Check if this manifest is due for execution
            if (SchedulingHelpers.ShouldRunNow(manifest, now, logger))
            {
                logger.LogDebug(
                    "Manifest {ManifestId} (name: {ManifestName}) is due for execution",
                    manifest.Id,
                    manifest.Name
                );
                manifestsToQueue.Add(manifest);
            }
        }

        // Evaluate dependent manifests (triggered by parent success, not by schedule)
        var dependentManifests = manifests
            .Where(m => m.ScheduleType == ScheduleType.Dependent && m.DependsOnManifestId != null)
            .ToList();

        if (dependentManifests.Count > 0)
        {
            logger.LogDebug(
                "Found {DependentCount} dependent manifests to evaluate",
                dependentManifests.Count
            );

            foreach (var dependent in dependentManifests)
            {
                if (ShouldSkipManifest(dependent, newlyDeadLetteredManifestIds))
                    continue;

                // Find parent manifest in the loaded set (only enabled manifests are loaded)
                var parent = manifests.FirstOrDefault(m => m.Id == dependent.DependsOnManifestId);
                if (parent is null)
                {
                    logger.LogTrace(
                        "Skipping dependent manifest {ManifestId} - parent manifest {ParentId} not found or disabled",
                        dependent.Id,
                        dependent.DependsOnManifestId
                    );
                    continue;
                }

                // Queue if parent's LastSuccessfulRun is newer than dependent's LastSuccessfulRun
                if (
                    parent.LastSuccessfulRun != null
                    && (
                        dependent.LastSuccessfulRun == null
                        || parent.LastSuccessfulRun > dependent.LastSuccessfulRun
                    )
                )
                {
                    logger.LogDebug(
                        "Dependent manifest {ManifestId} (name: {ManifestName}) is due - parent {ParentId} last succeeded at {ParentLastRun}",
                        dependent.Id,
                        dependent.Name,
                        parent.Id,
                        parent.LastSuccessfulRun
                    );
                    manifestsToQueue.Add(dependent);
                }
            }
        }

        logger.LogInformation(
            "DetermineJobsToQueueStep completed: {ManifestsToQueueCount} manifests are due for execution",
            manifestsToQueue.Count
        );

        return manifestsToQueue;
    }

    /// <summary>
    /// Checks common guards that apply to all manifest types (dead-lettered, queued, active execution).
    /// </summary>
    private bool ShouldSkipManifest(Manifest manifest, HashSet<int> newlyDeadLetteredManifestIds)
    {
        if (newlyDeadLetteredManifestIds.Contains(manifest.Id))
        {
            logger.LogTrace(
                "Skipping manifest {ManifestId} - was just dead-lettered in this cycle",
                manifest.Id
            );
            return true;
        }

        if (manifest.DeadLetters.Any(dl => dl.Status == DeadLetterStatus.AwaitingIntervention))
        {
            logger.LogTrace(
                "Skipping manifest {ManifestId} - has AwaitingIntervention dead letter",
                manifest.Id
            );
            return true;
        }

        if (manifest.WorkQueues.Any(q => q.Status == WorkQueueStatus.Queued))
        {
            logger.LogTrace(
                "Skipping manifest {ManifestId} - has queued work queue entry",
                manifest.Id
            );
            return true;
        }

        if (
            manifest.Metadatas.Any(
                m =>
                    m.WorkflowState == WorkflowState.Pending
                    || m.WorkflowState == WorkflowState.InProgress
            )
        )
        {
            logger.LogTrace(
                "Skipping manifest {ManifestId} - has pending or in-progress execution",
                manifest.Id
            );
            return true;
        }

        return false;
    }
}
