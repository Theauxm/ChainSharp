using System.Text.Json;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Utils;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;

/// <summary>
/// Dispatches queued work queue entries by creating Metadata records and enqueueing
/// them to the background task server.
/// </summary>
/// <remarks>
/// Enforces the global MaxActiveJobs limit at dispatch time by counting active
/// (Pending + InProgress) Metadata before dispatching. This is the correct
/// bottleneck since the JobDispatcher is the single gateway to the BackgroundTaskServer,
/// and all sources (ManifestManager, Dashboard, ManifestScheduler) write to the WorkQueue.
/// </remarks>
internal class DispatchJobsStep(
    IDataContext dataContext,
    IBackgroundTaskServer backgroundTaskServer,
    SchedulerConfiguration config,
    ILogger<DispatchJobsStep> logger
) : EffectStep<List<WorkQueue>, Unit>
{
    public override async Task<Unit> Run(List<WorkQueue> entries)
    {
        var dispatchStartTime = DateTime.UtcNow;
        var jobsDispatched = 0;

        logger.LogDebug("Starting DispatchJobsStep for {EntryCount} queued entries", entries.Count);

        // Enforce MaxActiveJobs at dispatch time
        if (config.MaxActiveJobs.HasValue)
        {
            // Exclude blacklisted workflow types (internal scheduler workflows by default).
            // Additional types can be excluded via ExcludeFromMaxActiveJobs<T>().
            var excluded = config.ExcludedWorkflowTypeNames;
            var activeMetadataCount = await dataContext.Metadatas.CountAsync(
                m =>
                    !excluded.Contains(m.Name)
                    && (
                        m.WorkflowState == WorkflowState.Pending
                        || m.WorkflowState == WorkflowState.InProgress
                    )
            );

            var remainingCapacity = config.MaxActiveJobs.Value - activeMetadataCount;

            if (remainingCapacity <= 0)
            {
                logger.LogInformation(
                    "MaxActiveJobs limit reached ({ActiveCount}/{MaxActiveJobs}). Skipping dispatch this cycle.",
                    activeMetadataCount,
                    config.MaxActiveJobs.Value
                );
                return Unit.Default;
            }

            entries = entries.Take(remainingCapacity).ToList();
            logger.LogDebug(
                "MaxActiveJobs capacity: {ActiveCount}/{MaxActiveJobs}, dispatching up to {RemainingCapacity} entries",
                activeMetadataCount,
                config.MaxActiveJobs.Value,
                remainingCapacity
            );
        }

        foreach (var entry in entries)
        {
            try
            {
                // Deserialize input if present
                object? deserializedInput = null;
                if (entry.Input != null && entry.InputTypeName != null)
                {
                    var inputType = ResolveType(entry.InputTypeName);
                    deserializedInput = JsonSerializer.Deserialize(
                        entry.Input,
                        inputType,
                        ChainSharpJsonSerializationOptions.ManifestProperties
                    );
                }

                // Create a new Metadata record for this execution
                var metadata = Models.Metadata.Metadata.Create(
                    new CreateMetadata
                    {
                        Name = entry.WorkflowName,
                        ExternalId = Guid.NewGuid().ToString("N"),
                        Input = null,
                        ManifestId = entry.ManifestId,
                    }
                );

                await dataContext.Track(metadata);
                await dataContext.SaveChanges(CancellationToken.None);

                // Update work queue entry
                entry.Status = WorkQueueStatus.Dispatched;
                entry.MetadataId = metadata.Id;
                entry.DispatchedAt = DateTime.UtcNow;
                await dataContext.SaveChanges(CancellationToken.None);

                logger.LogDebug(
                    "Created Metadata {MetadataId} for work queue entry {WorkQueueId} (workflow: {WorkflowName})",
                    metadata.Id,
                    entry.Id,
                    entry.WorkflowName
                );

                // Enqueue to background task server
                string backgroundTaskId;
                if (deserializedInput != null)
                    backgroundTaskId = await backgroundTaskServer.EnqueueAsync(
                        metadata.Id,
                        deserializedInput
                    );
                else
                    backgroundTaskId = await backgroundTaskServer.EnqueueAsync(metadata.Id);

                logger.LogInformation(
                    "Dispatched work queue entry {WorkQueueId} as background task {BackgroundTaskId} (Metadata: {MetadataId})",
                    entry.Id,
                    backgroundTaskId,
                    metadata.Id
                );

                jobsDispatched++;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error dispatching work queue entry {WorkQueueId} (workflow: {WorkflowName})",
                    entry.Id,
                    entry.WorkflowName
                );
            }
        }

        var duration = DateTime.UtcNow - dispatchStartTime;

        logger.LogInformation(
            "DispatchJobsStep completed: {JobsDispatched} jobs dispatched in {Duration}ms",
            jobsDispatched,
            duration.TotalMilliseconds
        );

        return Unit.Default;
    }

    private static Type ResolveType(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type != null)
            return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        throw new TypeLoadException($"Unable to find type: {typeName}");
    }
}
