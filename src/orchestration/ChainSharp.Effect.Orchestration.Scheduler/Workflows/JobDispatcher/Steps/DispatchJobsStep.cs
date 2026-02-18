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
/// Enforces both global MaxActiveJobs and per-group MaxActiveJobs limits at dispatch time.
/// Per-group limits prevent starvation: when a high-priority group hits its cap, lower-priority
/// groups can still dispatch (using <c>continue</c> instead of <c>break</c>).
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

        var excluded = config.ExcludedWorkflowTypeNames;

        // Count global active metadata
        var activeMetadataCount = await dataContext.Metadatas.CountAsync(
            m =>
                !excluded.Contains(m.Name)
                && (
                    m.WorkflowState == WorkflowState.Pending
                    || m.WorkflowState == WorkflowState.InProgress
                )
        );

        // Check global capacity
        if (config.MaxActiveJobs.HasValue && activeMetadataCount >= config.MaxActiveJobs.Value)
        {
            logger.LogInformation(
                "MaxActiveJobs limit reached ({ActiveCount}/{MaxActiveJobs}). Skipping dispatch this cycle.",
                activeMetadataCount,
                config.MaxActiveJobs.Value
            );
            return Unit.Default;
        }

        // Load per-group active counts
        var groupActiveCounts = await dataContext
            .Metadatas.Where(
                m =>
                    !excluded.Contains(m.Name)
                    && (
                        m.WorkflowState == WorkflowState.Pending
                        || m.WorkflowState == WorkflowState.InProgress
                    )
                    && m.ManifestId.HasValue
            )
            .Join(
                dataContext.Manifests,
                m => m.ManifestId!.Value,
                man => man.Id,
                (m, man) => man.ManifestGroupId
            )
            .GroupBy(gid => gid)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GroupId, g => g.Count);

        // Load per-group limits (only groups that have a limit set)
        var groupLimits = await dataContext
            .ManifestGroups.Where(g => g.MaxActiveJobs != null)
            .ToDictionaryAsync(g => g.Id, g => g.MaxActiveJobs!.Value);

        // Filter entries respecting both global and per-group limits
        var toDispatch = new List<WorkQueue>();
        var groupDispatchCounts = new Dictionary<int, int>();
        var globalDispatched = 0;

        foreach (var entry in entries) // already sorted by group priority
        {
            // Global limit check
            if (
                config.MaxActiveJobs.HasValue
                && activeMetadataCount + globalDispatched >= config.MaxActiveJobs.Value
            )
                break;

            // Per-group limit check
            var groupId = entry.Manifest.ManifestGroupId;
            if (groupLimits.TryGetValue(groupId, out var groupLimit))
            {
                var active = groupActiveCounts.GetValueOrDefault(groupId, 0);
                var dispatched = groupDispatchCounts.GetValueOrDefault(groupId, 0);
                if (active + dispatched >= groupLimit)
                    continue; // skip this group but keep processing other groups
            }

            toDispatch.Add(entry);
            groupDispatchCounts[groupId] = groupDispatchCounts.GetValueOrDefault(groupId, 0) + 1;
            globalDispatched++;
        }

        logger.LogDebug(
            "Dispatch selection: {ToDispatch} of {Total} entries (global active: {ActiveCount})",
            toDispatch.Count,
            entries.Count,
            activeMetadataCount
        );

        foreach (var entry in toDispatch)
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
