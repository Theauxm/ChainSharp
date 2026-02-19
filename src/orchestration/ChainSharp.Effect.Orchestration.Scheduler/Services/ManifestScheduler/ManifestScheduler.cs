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
        Action<ScheduleOptions>? options = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var resolved = ResolveOptions(options);

        using var context = CreateContext();

        var manifest = await context.UpsertManifestAsync<TWorkflow, TInput>(
            externalId,
            input,
            schedule,
            resolved.ManifestOptions,
            groupId: resolved.GroupId ?? externalId,
            groupPriority: resolved.GroupPriority,
            groupMaxActiveJobs: resolved.GroupMaxActiveJobs,
            groupIsEnabled: resolved.GroupEnabled,
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
        Action<ScheduleOptions>? options = null,
        Action<TSource, ManifestOptions>? configureEach = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var resolved = ResolveOptions(options);
        var sourceList = sources.ToList();

        if (sourceList.Count == 0)
            return [];

        using var context = CreateContext();
        var transaction = await context.BeginTransaction();

        try
        {
            var effectiveGroupId =
                resolved.GroupId
                ?? resolved.PrunePrefix
                ?? sourceList.Select(s => map(s).ExternalId).FirstOrDefault()
                ?? "batch";

            var results = new List<Manifest>(sourceList.Count);

            foreach (var source in sourceList)
            {
                var (externalId, input) = map(source);
                var itemOptions = CreateItemOptions(resolved.ManifestOptions);
                configureEach?.Invoke(source, itemOptions);

                var manifest = await context.UpsertManifestAsync<TWorkflow, TInput>(
                    externalId,
                    input,
                    schedule,
                    itemOptions,
                    groupId: effectiveGroupId,
                    groupPriority: resolved.GroupPriority,
                    groupMaxActiveJobs: resolved.GroupMaxActiveJobs,
                    groupIsEnabled: resolved.GroupEnabled,
                    ct: ct
                );
                results.Add(manifest);
            }

            await context.SaveChanges(ct);

            if (resolved.PrunePrefix is not null)
            {
                var keepIds = results.Select(m => m.ExternalId).ToHashSet();
                await PruneStaleManifestsAsync(context, resolved.PrunePrefix, keepIds, ct);
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
        Action<ScheduleOptions>? options = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var resolved = ResolveOptions(options);

        using var context = CreateContext();

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
            resolved.ManifestOptions,
            groupId: resolved.GroupId ?? externalId,
            groupPriority: resolved.GroupPriority,
            groupMaxActiveJobs: resolved.GroupMaxActiveJobs,
            groupIsEnabled: resolved.GroupEnabled,
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
        Action<ScheduleOptions>? options = null,
        Action<TSource, ManifestOptions>? configureEach = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var resolved = ResolveOptions(options);
        var sourceList = sources.ToList();

        if (sourceList.Count == 0)
            return [];

        using var context = CreateContext();
        var transaction = await context.BeginTransaction();

        try
        {
            var effectiveGroupId =
                resolved.GroupId
                ?? resolved.PrunePrefix
                ?? sourceList.Select(s => map(s).ExternalId).FirstOrDefault()
                ?? "batch";

            // Resolve all parent manifests in one query
            var parentExternalIds = sourceList.Select(dependsOn).Distinct().ToList();
            var parentManifests = await context
                .Manifests.Where(m => parentExternalIds.Contains(m.ExternalId))
                .ToDictionaryAsync(m => m.ExternalId, ct);

            var results = new List<Manifest>(sourceList.Count);

            foreach (var source in sourceList)
            {
                var (externalId, input) = map(source);
                var parentExternalId = dependsOn(source);

                if (!parentManifests.TryGetValue(parentExternalId, out var parentManifest))
                    throw new InvalidOperationException(
                        $"Parent manifest with ExternalId '{parentExternalId}' not found. "
                            + "Ensure parent manifests are scheduled before their dependents."
                    );

                var itemOptions = CreateItemOptions(resolved.ManifestOptions);
                configureEach?.Invoke(source, itemOptions);

                var manifest = await context.UpsertDependentManifestAsync<TWorkflow, TInput>(
                    externalId,
                    input,
                    parentManifest.Id,
                    itemOptions,
                    groupId: effectiveGroupId,
                    groupPriority: resolved.GroupPriority,
                    groupMaxActiveJobs: resolved.GroupMaxActiveJobs,
                    groupIsEnabled: resolved.GroupEnabled,
                    ct: ct
                );
                results.Add(manifest);
            }

            await context.SaveChanges(ct);

            if (resolved.PrunePrefix is not null)
            {
                var keepIds = results.Select(m => m.ExternalId).ToHashSet();
                await PruneStaleManifestsAsync(context, resolved.PrunePrefix, keepIds, ct);
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
        using var context = CreateContext();

        var manifest = await GetManifestByExternalIdAsync(context, externalId, ct);
        manifest.IsEnabled = false;
        await context.SaveChanges(ct);

        logger.LogInformation("Disabled manifest {ExternalId}", externalId);
    }

    /// <inheritdoc />
    public async Task EnableAsync(string externalId, CancellationToken ct = default)
    {
        using var context = CreateContext();

        var manifest = await GetManifestByExternalIdAsync(context, externalId, ct);
        manifest.IsEnabled = true;
        await context.SaveChanges(ct);

        logger.LogInformation("Enabled manifest {ExternalId}", externalId);
    }

    /// <inheritdoc />
    public async Task TriggerAsync(string externalId, CancellationToken ct = default)
    {
        using var context = CreateContext();

        var manifest = await GetManifestByExternalIdAsync(context, externalId, ct);

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

    // ── Private helpers ──────────────────────────────────────────────────

    private IDataContext CreateContext() =>
        dataContextFactory.Create() as IDataContext
        ?? throw new InvalidOperationException("Failed to create data context");

    private static ResolvedOptions ResolveOptions(Action<ScheduleOptions>? options)
    {
        var opts = new ScheduleOptions();
        options?.Invoke(opts);

        var manifestOptions = opts.ToManifestOptions();

        return new ResolvedOptions(
            ManifestOptions: manifestOptions,
            GroupId: opts._groupId,
            GroupPriority: opts._groupOptions?._priority ?? manifestOptions.Priority,
            GroupMaxActiveJobs: opts._groupOptions?._maxActiveJobs,
            GroupEnabled: opts._groupOptions?._isEnabled ?? true,
            PrunePrefix: opts._prunePrefix
        );
    }

    private static ManifestOptions CreateItemOptions(ManifestOptions baseOptions) =>
        new()
        {
            Priority = baseOptions.Priority,
            IsEnabled = baseOptions.IsEnabled,
            MaxRetries = baseOptions.MaxRetries,
            Timeout = baseOptions.Timeout,
            IsDormant = baseOptions.IsDormant,
        };

    private static async Task<Manifest> GetManifestByExternalIdAsync(
        IDataContext context,
        string externalId,
        CancellationToken ct
    ) =>
        await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
        ?? throw new InvalidOperationException($"No manifest found with ExternalId '{externalId}'");

    private async Task PruneStaleManifestsAsync(
        IDataContext context,
        string prunePrefix,
        System.Collections.Generic.HashSet<string> keepExternalIds,
        CancellationToken ct
    )
    {
        var staleManifestIds = await context
            .Manifests.Where(
                m => m.ExternalId.StartsWith(prunePrefix) && !keepExternalIds.Contains(m.ExternalId)
            )
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (staleManifestIds.Count == 0)
            return;

        // Delete in FK-dependency order: dead_letters → metadata → manifests
        await context
            .DeadLetters.Where(d => staleManifestIds.Contains(d.ManifestId))
            .ExecuteDeleteAsync(ct);

        await context
            .Metadatas.Where(
                m => m.ManifestId.HasValue && staleManifestIds.Contains(m.ManifestId.Value)
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

    private record ResolvedOptions(
        ManifestOptions ManifestOptions,
        string? GroupId,
        int GroupPriority,
        int? GroupMaxActiveJobs,
        bool GroupEnabled,
        string? PrunePrefix
    );
}
