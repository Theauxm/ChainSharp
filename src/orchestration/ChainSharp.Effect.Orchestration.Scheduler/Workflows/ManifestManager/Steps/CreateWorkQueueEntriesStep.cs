using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.WorkQueue.DTOs;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager.Steps;

/// <summary>
/// Creates work queue entries for manifests that are due to run.
/// </summary>
/// <remarks>
/// This step replaces the previous EnqueueJobsStep. Instead of directly creating Metadata
/// records and enqueueing to the background task server, it creates WorkQueue entries
/// that will be picked up by the JobDispatcherWorkflow.
/// </remarks>
internal class CreateWorkQueueEntriesStep(
    IDataContext dataContext,
    SchedulerConfiguration schedulerConfiguration,
    ILogger<CreateWorkQueueEntriesStep> logger
) : EffectStep<List<Manifest>, Unit>
{
    public override async Task<Unit> Run(List<Manifest> manifests)
    {
        var pollStartTime = DateTime.UtcNow;
        var entriesCreated = 0;

        logger.LogDebug(
            "Starting CreateWorkQueueEntriesStep for {ManifestCount} manifests",
            manifests.Count
        );

        foreach (var manifest in manifests)
        {
            try
            {
                var basePriority = manifest.ManifestGroup.Priority;
                var effectivePriority =
                    manifest.ScheduleType == ScheduleType.Dependent
                        ? basePriority + schedulerConfiguration.DependentPriorityBoost
                        : basePriority;

                var entry = Models.WorkQueue.WorkQueue.Create(
                    new CreateWorkQueue
                    {
                        WorkflowName = manifest.Name,
                        Input = manifest.Properties,
                        InputTypeName = manifest.PropertyTypeName,
                        ManifestId = manifest.Id,
                        Priority = effectivePriority,
                    }
                );

                await dataContext.Track(entry);
                await dataContext.SaveChanges(CancellationToken.None);

                logger.LogDebug(
                    "Created WorkQueue entry {WorkQueueId} for manifest {ManifestId} (name: {ManifestName})",
                    entry.Id,
                    manifest.Id,
                    manifest.Name
                );

                entriesCreated++;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error creating work queue entry for manifest {ManifestId} (name: {ManifestName})",
                    manifest.Id,
                    manifest.Name
                );
            }
        }

        var duration = DateTime.UtcNow - pollStartTime;

        logger.LogInformation(
            "CreateWorkQueueEntriesStep completed: {EntriesCreated} entries created in {Duration}ms",
            entriesCreated,
            duration.TotalMilliseconds
        );

        return Unit.Default;
    }
}
