using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Models.WorkQueue.DTOs;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Schedule = ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;

/// <summary>
/// Implementation of <see cref="IManifestScheduler"/> that provides type-safe manifest scheduling.
/// </summary>
public class ManifestScheduler(
    IDataContextProviderFactory dataContextFactory,
    IWorkflowRegistry workflowRegistry,
    ILogger<ManifestScheduler> logger
) : IManifestScheduler
{
    /// <inheritdoc />
    public async Task<Manifest> ScheduleAsync<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var options = new ManifestOptions { Priority = priority };
        configure?.Invoke(options);

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest = await context.UpsertManifestAsync<TWorkflow, TInput>(
            externalId,
            input,
            schedule,
            options,
            groupId: groupId ?? externalId,
            ct: ct
        );

        await context.SaveChanges(ct);

        logger.LogInformation(
            "Scheduled workflow {Workflow} with ExternalId {ExternalId}",
            typeof(TWorkflow).Name,
            externalId
        );

        return manifest;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Manifest>> ScheduleManyAsync<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        string? groupId = null,
        int priority = 0,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var results = new List<Manifest>();
        var sourceList = sources.ToList();

        if (sourceList.Count == 0)
            return results;

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var transaction = await context.BeginTransaction();

        try
        {
            var effectiveGroupId =
                groupId
                ?? prunePrefix
                ?? sourceList.Select(s => map(s).ExternalId).FirstOrDefault()
                ?? "batch";

            foreach (var source in sourceList)
            {
                var (externalId, input) = map(source);
                var options = new ManifestOptions { Priority = priority };
                configure?.Invoke(source, options);

                var manifest = await context.UpsertManifestAsync<TWorkflow, TInput>(
                    externalId,
                    input,
                    schedule,
                    options,
                    groupId: effectiveGroupId,
                    ct: ct
                );
                results.Add(manifest);
            }

            await context.SaveChanges(ct);

            if (prunePrefix is not null)
            {
                var keepIds = results.Select(m => m.ExternalId).ToHashSet();

                // Collect the manifest IDs to prune
                var staleManifestIds = await context
                    .Manifests.Where(
                        m => m.ExternalId.StartsWith(prunePrefix) && !keepIds.Contains(m.ExternalId)
                    )
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                if (staleManifestIds.Count > 0)
                {
                    // Delete in FK-dependency order: dead_letters → metadata → manifests
                    await context
                        .DeadLetters.Where(d => staleManifestIds.Contains(d.ManifestId))
                        .ExecuteDeleteAsync(ct);

                    await context
                        .Metadatas.Where(
                            m =>
                                m.ManifestId.HasValue
                                && staleManifestIds.Contains(m.ManifestId.Value)
                        )
                        .ExecuteDeleteAsync(ct);

                    var pruned = await context
                        .Manifests.Where(m => staleManifestIds.Contains(m.Id))
                        .ExecuteDeleteAsync(ct);

                    logger.LogInformation(
                        "Pruned {Count} stale manifests with prefix '{Prefix}'",
                        pruned,
                        prunePrefix
                    );
                }
            }

            await context.CommitTransaction();

            logger.LogInformation(
                "Scheduled {Count} manifests for workflow {Workflow} in single transaction",
                results.Count,
                typeof(TWorkflow).Name
            );

            return results;
        }
        catch
        {
            await context.RollbackTransaction();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<Manifest> ScheduleDependentAsync<TWorkflow, TInput>(
        string externalId,
        TInput input,
        string dependsOnExternalId,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var options = new ManifestOptions { Priority = priority };
        configure?.Invoke(options);

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var parentManifest =
            await context.Manifests.FirstOrDefaultAsync(
                m => m.ExternalId == dependsOnExternalId,
                ct
            )
            ?? throw new InvalidOperationException(
                $"Parent manifest with ExternalId '{dependsOnExternalId}' not found. "
                    + "Ensure the parent manifest is scheduled before its dependents."
            );

        var manifest = await context.UpsertDependentManifestAsync<TWorkflow, TInput>(
            externalId,
            input,
            parentManifest.Id,
            options,
            groupId: groupId ?? externalId,
            ct: ct
        );

        await context.SaveChanges(ct);

        logger.LogInformation(
            "Scheduled dependent workflow {Workflow} with ExternalId {ExternalId} depending on {ParentExternalId}",
            typeof(TWorkflow).Name,
            externalId,
            dependsOnExternalId
        );

        return manifest;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Manifest>> ScheduleManyDependentAsync<
        TWorkflow,
        TInput,
        TSource
    >(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Func<TSource, string> dependsOn,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        string? groupId = null,
        int priority = 0,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var results = new List<Manifest>();
        var sourceList = sources.ToList();

        if (sourceList.Count == 0)
            return results;

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var transaction = await context.BeginTransaction();

        try
        {
            var effectiveGroupId =
                groupId
                ?? prunePrefix
                ?? sourceList.Select(s => map(s).ExternalId).FirstOrDefault()
                ?? "batch";

            // Collect all unique parent external IDs and resolve them in one pass
            var parentExternalIds = sourceList.Select(dependsOn).Distinct().ToList();
            var parentManifests = await context
                .Manifests.Where(m => parentExternalIds.Contains(m.ExternalId))
                .ToDictionaryAsync(m => m.ExternalId, ct);

            foreach (var source in sourceList)
            {
                var (externalId, input) = map(source);
                var parentExternalId = dependsOn(source);

                if (!parentManifests.TryGetValue(parentExternalId, out var parentManifest))
                    throw new InvalidOperationException(
                        $"Parent manifest with ExternalId '{parentExternalId}' not found. "
                            + "Ensure parent manifests are scheduled before their dependents."
                    );

                var options = new ManifestOptions { Priority = priority };
                configure?.Invoke(source, options);

                var manifest = await context.UpsertDependentManifestAsync<TWorkflow, TInput>(
                    externalId,
                    input,
                    parentManifest.Id,
                    options,
                    groupId: effectiveGroupId,
                    ct: ct
                );
                results.Add(manifest);
            }

            await context.SaveChanges(ct);

            if (prunePrefix is not null)
            {
                var keepIds = results.Select(m => m.ExternalId).ToHashSet();

                var staleManifestIds = await context
                    .Manifests.Where(
                        m => m.ExternalId.StartsWith(prunePrefix) && !keepIds.Contains(m.ExternalId)
                    )
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                if (staleManifestIds.Count > 0)
                {
                    await context
                        .DeadLetters.Where(d => staleManifestIds.Contains(d.ManifestId))
                        .ExecuteDeleteAsync(ct);

                    await context
                        .Metadatas.Where(
                            m =>
                                m.ManifestId.HasValue
                                && staleManifestIds.Contains(m.ManifestId.Value)
                        )
                        .ExecuteDeleteAsync(ct);

                    var pruned = await context
                        .Manifests.Where(m => staleManifestIds.Contains(m.Id))
                        .ExecuteDeleteAsync(ct);

                    logger.LogInformation(
                        "Pruned {Count} stale dependent manifests with prefix '{Prefix}'",
                        pruned,
                        prunePrefix
                    );
                }
            }

            await context.CommitTransaction();

            logger.LogInformation(
                "Scheduled {Count} dependent manifests for workflow {Workflow} in single transaction",
                results.Count,
                typeof(TWorkflow).Name
            );

            return results;
        }
        catch
        {
            await context.RollbackTransaction();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task DisableAsync(string externalId, CancellationToken ct = default)
    {
        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest =
            await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
            ?? throw new InvalidOperationException(
                $"No manifest found with ExternalId '{externalId}'"
            );

        manifest.IsEnabled = false;
        await context.SaveChanges(ct);

        logger.LogInformation("Disabled manifest {ExternalId}", externalId);
    }

    /// <inheritdoc />
    public async Task EnableAsync(string externalId, CancellationToken ct = default)
    {
        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest =
            await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
            ?? throw new InvalidOperationException(
                $"No manifest found with ExternalId '{externalId}'"
            );

        manifest.IsEnabled = true;
        await context.SaveChanges(ct);

        logger.LogInformation("Enabled manifest {ExternalId}", externalId);
    }

    /// <inheritdoc />
    public async Task TriggerAsync(string externalId, CancellationToken ct = default)
    {
        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest =
            await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
            ?? throw new InvalidOperationException(
                $"No manifest found with ExternalId '{externalId}'"
            );

        // Create a work queue entry instead of directly creating metadata + enqueueing
        var entry = WorkQueue.Create(
            new CreateWorkQueue
            {
                WorkflowName = manifest.Name,
                Input = manifest.Properties,
                InputTypeName = manifest.PropertyTypeName,
                ManifestId = manifest.Id,
                Priority = manifest.Priority,
            }
        );
        context.WorkQueues.Add(entry);
        await context.SaveChanges(ct);

        logger.LogInformation(
            "Queued manifest {ExternalId} for execution (WorkQueueId: {WorkQueueId})",
            externalId,
            entry.Id
        );
    }
}
