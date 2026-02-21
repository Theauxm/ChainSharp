using System.Text.Json;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Utils;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;

/// <summary>
/// Creates Metadata records and enqueues each entry to the background task server.
/// </summary>
/// <remarks>
/// Each entry is dispatched within its own DI scope and database transaction,
/// using <c>FOR UPDATE SKIP LOCKED</c> to atomically claim the work queue entry.
/// This ensures safe concurrent dispatch across multiple server instances.
/// </remarks>
internal class DispatchJobsStep(IServiceProvider serviceProvider, ILogger<DispatchJobsStep> logger)
    : EffectStep<List<WorkQueue>, Unit>
{
    public override async Task<Unit> Run(List<WorkQueue> entries)
    {
        var dispatchStartTime = DateTime.UtcNow;
        var jobsDispatched = 0;

        logger.LogDebug("Starting DispatchJobsStep for {EntryCount} entries", entries.Count);

        foreach (var entry in entries)
        {
            try
            {
                var dispatched = await TryClaimAndDispatchAsync(entry);

                if (dispatched)
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

        if (jobsDispatched > 0)
            logger.LogInformation(
                "DispatchJobsStep completed: {JobsDispatched} jobs dispatched in {Duration}ms",
                jobsDispatched,
                duration.TotalMilliseconds
            );
        else
            logger.LogDebug("DispatchJobsStep completed: no jobs dispatched");

        return Unit.Default;
    }

    /// <summary>
    /// Atomically claims a work queue entry using FOR UPDATE SKIP LOCKED,
    /// creates its Metadata record, and enqueues to the background task server.
    /// </summary>
    /// <returns>True if the entry was successfully dispatched; false if it was already claimed.</returns>
    private async Task<bool> TryClaimAndDispatchAsync(WorkQueue entry)
    {
        using var scope = serviceProvider.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var backgroundTaskServer =
            scope.ServiceProvider.GetRequiredService<IBackgroundTaskServer>();

        using var transaction = await dataContext.BeginTransaction();

        // Atomically claim the entry — skips entries locked by other dispatchers
        var claimed = await dataContext
            .WorkQueues.FromSqlRaw(
                """
                SELECT * FROM chain_sharp.work_queue
                WHERE id = {0} AND status = 'queued'
                FOR UPDATE SKIP LOCKED
                """,
                entry.Id
            )
            .FirstOrDefaultAsync();

        if (claimed is null)
        {
            await dataContext.RollbackTransaction();
            logger.LogDebug(
                "Work queue entry {WorkQueueId} already claimed by another server, skipping",
                entry.Id
            );
            return false;
        }

        // Deserialize input if present
        object? deserializedInput = null;
        if (claimed is { Input: not null, InputTypeName: not null })
        {
            var inputType = ResolveType(claimed.InputTypeName);
            deserializedInput = JsonSerializer.Deserialize(
                claimed.Input,
                inputType,
                ChainSharpJsonSerializationOptions.ManifestProperties
            );
        }

        // Create a new Metadata record for this execution
        var metadata = Models.Metadata.Metadata.Create(
            new CreateMetadata
            {
                Name = claimed.WorkflowName,
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = null,
                ManifestId = claimed.ManifestId
            }
        );

        await dataContext.Track(metadata);
        await dataContext.SaveChanges(CancellationToken.None);

        // Update work queue entry
        claimed.Status = WorkQueueStatus.Dispatched;
        claimed.MetadataId = metadata.Id;
        claimed.DispatchedAt = DateTime.UtcNow;
        await dataContext.SaveChanges(CancellationToken.None);

        // Enqueue to background task server.
        // For PostgresTaskServer (same scope/context), the BackgroundJob insert
        // is part of this transaction — committed atomically below.
        string backgroundTaskId;
        if (deserializedInput != null)
            backgroundTaskId = await backgroundTaskServer.EnqueueAsync(
                metadata.Id,
                deserializedInput
            );
        else
            backgroundTaskId = await backgroundTaskServer.EnqueueAsync(metadata.Id);

        await dataContext.CommitTransaction();

        logger.LogDebug(
            "Dispatched work queue entry {WorkQueueId} as background task {BackgroundTaskId} (Metadata: {MetadataId})",
            entry.Id,
            backgroundTaskId,
            metadata.Id
        );

        return true;
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
