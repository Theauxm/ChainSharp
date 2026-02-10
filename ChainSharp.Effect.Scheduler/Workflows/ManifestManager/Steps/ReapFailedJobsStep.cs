using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.DeadLetter.DTOs;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Services.EffectStep;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Scheduler.Workflows.ManifestManager.Steps;

/// <summary>
/// Reaps failed jobs by creating DeadLetter records for manifests that exceed their retry limit.
/// </summary>
/// <remarks>
/// This step receives manifests from LoadManifestsStep and identifies those that have
/// exceeded their max_retries count, moving them into the dead letter queue for manual intervention.
///
/// Dead letters are persisted immediately via SaveChanges() to ensure they survive
/// even if later steps in the workflow fail.
///
/// The returned List&lt;DeadLetter&gt; is stored in the workflow's Memory and made available
/// to DetermineJobsToQueueStep so it can exclude just-dead-lettered manifests.
/// </remarks>
internal class ReapFailedJobsStep(IDataContext dataContext, ILogger<ReapFailedJobsStep> logger)
    : EffectStep<List<Manifest>, List<DeadLetter>>
{
    public override async Task<List<DeadLetter>> Run(List<Manifest> manifests)
    {
        logger.LogDebug("Starting ReapFailedJobsStep to identify and dead-letter failed jobs");

        var deadLettersCreated = new List<DeadLetter>();

        logger.LogDebug(
            "Evaluating {ManifestCount} enabled manifests for dead-lettering",
            manifests.Count
        );

        foreach (var manifest in manifests)
        {
            // Check if any dead letter already exists for this manifest (AwaitingIntervention)
            var existingDeadLetter = manifest.DeadLetters.FirstOrDefault(
                dl => dl.Status == DeadLetterStatus.AwaitingIntervention
            );

            if (existingDeadLetter != null)
            {
                logger.LogTrace(
                    "Skipping manifest {ManifestId}: already has AwaitingIntervention dead letter",
                    manifest.Id
                );
                continue;
            }

            // Count failed executions for this manifest using in-memory data
            var failedCount = manifest.Metadatas.Count(
                m => m.WorkflowState == WorkflowState.Failed
            );

            if (failedCount >= manifest.MaxRetries)
            {
                logger.LogWarning(
                    "Manifest {ManifestId} (name: {ManifestName}) exceeds max retries ({FailedCount}/{MaxRetries}). Creating dead letter.",
                    manifest.Id,
                    manifest.Name,
                    failedCount,
                    manifest.MaxRetries
                );

                var deadLetter = DeadLetter.Create(
                    new CreateDeadLetter
                    {
                        Manifest = manifest,
                        Reason =
                            $"Max retries exceeded: ({failedCount}) failures >= ({manifest.MaxRetries}) max retries",
                        RetryCount = failedCount
                    }
                );

                await dataContext.Track(deadLetter);
                deadLettersCreated.Add(deadLetter);
            }
        }

        // Persist all changes immediately to ensure dead letters survive workflow failure
        await dataContext.SaveChanges(CancellationToken.None);

        logger.LogInformation(
            "ReapFailedJobsStep completed: {DeadLettersCreated} dead letters created",
            deadLettersCreated.Count
        );

        return deadLettersCreated;
    }
}
