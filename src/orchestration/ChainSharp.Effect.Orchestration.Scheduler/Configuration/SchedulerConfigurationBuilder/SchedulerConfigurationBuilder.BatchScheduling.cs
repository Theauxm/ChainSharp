using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Schedule = ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Orchestration.Scheduler.Configuration;

public partial class SchedulerConfigurationBuilder
{
    /// <summary>
    /// Schedules multiple instances of a workflow from a collection.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="sources">The collection of source items to create manifests from</param>
    /// <param name="map">A function that transforms each source item into an ExternalId and Input pair</param>
    /// <param name="schedule">The schedule definition applied to all manifests</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="priority">The dispatch priority (0-31, default 0) applied to all items. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// The manifests are not created immediately. They are captured and will be seeded
    /// automatically on startup by the ManifestPollingService.
    /// All manifests are created in a single transaction.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddChainSharpEffects(options => options
    ///     .AddScheduler(scheduler => scheduler
    ///         .UseHangfire(/* ... */)
    ///         .ScheduleMany&lt;ISyncTableWorkflow, SyncTableInput, string&gt;(
    ///             new[] { "users", "orders", "products" },
    ///             table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    ///             Every.Minutes(5))
    ///     )
    /// );
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder ScheduleMany<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        // Materialize the sources to avoid multiple enumeration
        var sourceList = sources.ToList();
        var firstId = sourceList.Select(s => map(s).ExternalId).FirstOrDefault() ?? "batch";

        foreach (var source in sourceList)
        {
            var (extId, _) = map(source);
            _externalIdToGroupId[extId] = groupId ?? extId;
        }

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = $"{firstId}... (batch of {sourceList.Count})",
                ScheduleFunc = async (scheduler, ct) =>
                {
                    var results = await scheduler.ScheduleManyAsync<TWorkflow, TInput, TSource>(
                        sourceList,
                        map,
                        schedule,
                        configure,
                        prunePrefix: prunePrefix,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    );
                    return results.FirstOrDefault()!;
                }
            }
        );

        _rootScheduledExternalId = null;
        _lastScheduledExternalId = null;

        return this;
    }

    /// <summary>
    /// Schedules multiple instances of a workflow from a collection using a name-based convention.
    /// The <paramref name="name"/> automatically derives <c>groupId</c>, <c>prunePrefix</c>, and
    /// the external ID prefix — reducing boilerplate when the naming follows the <c>{name}-{suffix}</c> pattern.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="name">The batch name. Used as <c>groupId</c>, <c>prunePrefix</c> is <c>"{name}-"</c>, and each external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="sources">The collection of items to create manifests from</param>
    /// <param name="map">A function that transforms each source item into a <c>Suffix</c> and <c>Input</c> pair. The full external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="schedule">The schedule definition applied to all manifests</param>
    /// <param name="configure">Optional callback to set per-item manifest options</param>
    /// <param name="priority">The dispatch priority (0-31, default 0) applied to all items. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <example>
    /// <code>
    /// scheduler.ScheduleMany&lt;ISyncTableWorkflow, SyncTableInput, string&gt;(
    ///     "sync-table",
    ///     new[] { "users", "orders" },
    ///     table => (table, new SyncTableInput { TableName = table }),
    ///     Every.Minutes(5));
    /// // Creates manifests: sync-table-users, sync-table-orders
    /// // groupId: "sync-table", prunePrefix: "sync-table-"
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder ScheduleMany<TWorkflow, TInput, TSource>(
        string name,
        IEnumerable<TSource> sources,
        Func<TSource, (string Suffix, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties =>
        ScheduleMany<TWorkflow, TInput, TSource>(
            sources,
            source =>
            {
                var (suffix, input) = map(source);
                return ($"{name}-{suffix}", input);
            },
            schedule,
            configure,
            prunePrefix: $"{name}-",
            groupId: name,
            priority: priority
        );

    /// <summary>
    /// Schedules multiple dependent workflow instances for deeper chaining after a previous
    /// <see cref="IncludeMany{TWorkflow,TInput,TSource}(IEnumerable{TSource},Func{TSource,ValueTuple{string,TInput}},Func{TSource,string},Action{TSource,ManifestOptions},string,string,int)"/>.
    /// Each dependent manifest is linked to its parent via the <paramref name="dependsOn"/> function.
    /// For first-level batch dependents after <see cref="ScheduleMany{TWorkflow,TInput,TSource}"/>,
    /// use the <c>IncludeMany</c> overload with <c>dependsOn</c> instead.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="sources">The collection of source items to create dependent manifests from</param>
    /// <param name="map">A function that transforms each source item into an ExternalId and Input pair</param>
    /// <param name="dependsOn">A function that maps each source item to the external ID of its parent manifest</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="prunePrefix">When specified, deletes stale manifests with this prefix not in the current batch</param>
    /// <param name="groupId">Optional group identifier for dashboard grouping</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0) applied to all items. DependentPriorityBoost is added on top at dispatch time. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <example>
    /// <code>
    /// scheduler
    ///     .ScheduleMany&lt;IExtractWorkflow, ExtractInput, int&gt;(...)
    ///     .IncludeMany&lt;ITransformWorkflow, TransformInput, int&gt;(
    ///         ..., dependsOn: i => $"extract-{i}")
    ///     .ThenIncludeMany&lt;ILoadWorkflow, LoadInput, int&gt;(
    ///         Enumerable.Range(0, 10),
    ///         i => ($"load-{i}", new LoadInput { Index = i }),
    ///         dependsOn: i => $"transform-{i}",
    ///         groupId: "load")
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder ThenIncludeMany<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Func<TSource, string> dependsOn,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var sourceList = sources.ToList();
        var firstId = sourceList.Select(s => map(s).ExternalId).FirstOrDefault() ?? "batch";

        foreach (var source in sourceList)
        {
            var (extId, _) = map(source);
            var parentExtId = dependsOn(source);
            _externalIdToGroupId[extId] = groupId ?? extId;
            _dependencyEdges.Add((parentExtId, extId));
        }

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = $"{firstId}... (dependent batch of {sourceList.Count})",
                ScheduleFunc = async (scheduler, ct) =>
                {
                    var results = await scheduler.ScheduleManyDependentAsync<
                        TWorkflow,
                        TInput,
                        TSource
                    >(
                        sourceList,
                        map,
                        dependsOn,
                        configure,
                        prunePrefix: prunePrefix,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    );
                    return results.FirstOrDefault()!;
                }
            }
        );

        _lastScheduledExternalId = null;

        return this;
    }

    /// <summary>
    /// Name-based overload of <see cref="ThenIncludeMany{TWorkflow,TInput,TSource}(IEnumerable{TSource},Func{TSource,ValueTuple{string,TInput}},Func{TSource,string},Action{TSource,ManifestOptions},string,string,int)"/>.
    /// The <paramref name="name"/> automatically derives <c>groupId</c>, <c>prunePrefix</c>, and
    /// the external ID prefix — reducing boilerplate when the naming follows the <c>{name}-{suffix}</c> pattern.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="name">The batch name. Used as <c>groupId</c>, <c>prunePrefix</c> is <c>"{name}-"</c>, and each external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="sources">The collection of source items to create dependent manifests from</param>
    /// <param name="map">A function that transforms each source item into a <c>Suffix</c> and <c>Input</c> pair. The full external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="dependsOn">A function that maps each source item to the external ID of its parent manifest</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <example>
    /// <code>
    /// scheduler
    ///     .ScheduleMany&lt;IExtractWorkflow, ExtractInput, int&gt;("extract", ...)
    ///     .IncludeMany&lt;ITransformWorkflow, TransformInput, int&gt;("transform",
    ///         ..., dependsOn: i => $"extract-{i}")
    ///     .ThenIncludeMany&lt;ILoadWorkflow, LoadInput, int&gt;("load",
    ///         Enumerable.Range(0, 10),
    ///         i => ($"{i}", new LoadInput { Index = i }),
    ///         dependsOn: i => $"transform-{i}")
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder ThenIncludeMany<TWorkflow, TInput, TSource>(
        string name,
        IEnumerable<TSource> sources,
        Func<TSource, (string Suffix, TInput Input)> map,
        Func<TSource, string> dependsOn,
        Action<TSource, ManifestOptions>? configure = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties =>
        ThenIncludeMany<TWorkflow, TInput, TSource>(
            sources,
            source =>
            {
                var (suffix, input) = map(source);
                return ($"{name}-{suffix}", input);
            },
            dependsOn,
            configure,
            prunePrefix: $"{name}-",
            groupId: name,
            priority: priority
        );

    /// <summary>
    /// Schedules multiple dependent workflow instances that each depend on the root <see cref="Schedule{TWorkflow,TInput}"/> manifest.
    /// Unlike <see cref="ThenIncludeMany{TWorkflow,TInput,TSource}"/> which requires an explicit <c>dependsOn</c> function,
    /// <c>IncludeMany</c> automatically parents all items from the root <c>Schedule</c>, enabling fan-out patterns.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="sources">The collection of source items to create dependent manifests from</param>
    /// <param name="map">A function that transforms each source item into an ExternalId and Input pair</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="prunePrefix">When specified, deletes stale manifests with this prefix not in the current batch</param>
    /// <param name="groupId">Optional group identifier for dashboard grouping</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// Must be called after <see cref="Schedule{TWorkflow,TInput}"/>.
    /// <code>
    /// .Schedule&lt;A&gt;("root", inputA, Every.Minutes(5))
    /// .IncludeMany&lt;B, InputB, int&gt;(
    ///     Enumerable.Range(0, 10),
    ///     i =&gt; ($"child-{i}", new InputB { Index = i }),
    ///     groupId: "children")
    /// // All 10 child manifests depend on "root" (A)
    /// </code>
    /// </remarks>
    public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var rootExternalId =
            _rootScheduledExternalId
            ?? throw new InvalidOperationException(
                "IncludeMany() must be called after Schedule(). "
                    + "No root manifest external ID is available."
            );

        var sourceList = sources.ToList();
        var firstId = sourceList.Select(s => map(s).ExternalId).FirstOrDefault() ?? "batch";

        foreach (var source in sourceList)
        {
            var (extId, _) = map(source);
            _externalIdToGroupId[extId] = groupId ?? extId;
            _dependencyEdges.Add((rootExternalId, extId));
        }

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = $"{firstId}... (dependent batch of {sourceList.Count})",
                ScheduleFunc = async (scheduler, ct) =>
                {
                    var results = await scheduler.ScheduleManyDependentAsync<
                        TWorkflow,
                        TInput,
                        TSource
                    >(
                        sourceList,
                        map,
                        _ => rootExternalId,
                        configure,
                        prunePrefix: prunePrefix,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    );
                    return results.FirstOrDefault()!;
                }
            }
        );

        _lastScheduledExternalId = null;

        return this;
    }

    /// <summary>
    /// Name-based overload of <see cref="IncludeMany{TWorkflow,TInput,TSource}(IEnumerable{TSource},Func{TSource,ValueTuple{string,TInput}},Action{TSource,ManifestOptions},string,string,int)"/>.
    /// The <paramref name="name"/> automatically derives <c>groupId</c>, <c>prunePrefix</c>, and
    /// the external ID prefix — reducing boilerplate when the naming follows the <c>{name}-{suffix}</c> pattern.
    /// All items automatically depend on the root <see cref="Schedule{TWorkflow,TInput}"/> manifest.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="name">The batch name. Used as <c>groupId</c>, <c>prunePrefix</c> is <c>"{name}-"</c>, and each external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="sources">The collection of source items to create dependent manifests from</param>
    /// <param name="map">A function that transforms each source item into a <c>Suffix</c> and <c>Input</c> pair. The full external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <example>
    /// <code>
    /// scheduler
    ///     .Schedule&lt;IExtractWorkflow, ExtractInput&gt;(
    ///         "extract-all", new ExtractInput(), Every.Hours(1))
    ///     .IncludeMany&lt;ILoadWorkflow, LoadInput, int&gt;("load",
    ///         Enumerable.Range(0, 10),
    ///         i => ($"{i}", new LoadInput { Partition = i }))
    /// // Creates: load-0, load-1, ... load-9 (all depend on extract-all)
    /// // groupId: "load", prunePrefix: "load-"
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
        string name,
        IEnumerable<TSource> sources,
        Func<TSource, (string Suffix, TInput Input)> map,
        Action<TSource, ManifestOptions>? configure = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties =>
        IncludeMany<TWorkflow, TInput, TSource>(
            sources,
            source =>
            {
                var (suffix, input) = map(source);
                return ($"{name}-{suffix}", input);
            },
            configure,
            prunePrefix: $"{name}-",
            groupId: name,
            priority: priority
        );

    /// <summary>
    /// Schedules multiple dependent workflow instances from a collection, where each item
    /// explicitly maps to its parent via the <paramref name="dependsOn"/> function.
    /// Use after <see cref="ScheduleMany{TWorkflow,TInput,TSource}"/> for first-level batch dependents,
    /// or after <see cref="Schedule{TWorkflow,TInput}"/> when explicit per-item parent mapping is needed.
    /// For deeper chaining after a previous <c>IncludeMany</c>, use <see cref="ThenIncludeMany{TWorkflow,TInput,TSource}"/>.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="sources">The collection of source items to create dependent manifests from</param>
    /// <param name="map">A function that transforms each source item into an ExternalId and Input pair</param>
    /// <param name="dependsOn">A function that maps each source item to the external ID of its parent manifest</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="prunePrefix">When specified, deletes stale manifests with this prefix not in the current batch</param>
    /// <param name="groupId">Optional group identifier for dashboard grouping</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <example>
    /// <code>
    /// scheduler
    ///     .ScheduleMany&lt;IExtractWorkflow, ExtractInput, int&gt;(
    ///         Enumerable.Range(0, 10),
    ///         i => ($"extract-{i}", new ExtractInput { Index = i }),
    ///         Every.Minutes(5),
    ///         groupId: "extract")
    ///     .IncludeMany&lt;ITransformWorkflow, TransformInput, int&gt;(
    ///         Enumerable.Range(0, 10),
    ///         i => ($"transform-{i}", new TransformInput { Index = i }),
    ///         dependsOn: i => $"extract-{i}",
    ///         groupId: "transform")
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Func<TSource, string> dependsOn,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var sourceList = sources.ToList();
        var firstId = sourceList.Select(s => map(s).ExternalId).FirstOrDefault() ?? "batch";

        foreach (var source in sourceList)
        {
            var (extId, _) = map(source);
            var parentExtId = dependsOn(source);
            _externalIdToGroupId[extId] = groupId ?? extId;
            _dependencyEdges.Add((parentExtId, extId));
        }

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = $"{firstId}... (dependent batch of {sourceList.Count})",
                ScheduleFunc = async (scheduler, ct) =>
                {
                    var results = await scheduler.ScheduleManyDependentAsync<
                        TWorkflow,
                        TInput,
                        TSource
                    >(
                        sourceList,
                        map,
                        dependsOn,
                        configure,
                        prunePrefix: prunePrefix,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    );
                    return results.FirstOrDefault()!;
                }
            }
        );

        _lastScheduledExternalId = null;

        return this;
    }

    /// <summary>
    /// Name-based overload of <see cref="IncludeMany{TWorkflow,TInput,TSource}(IEnumerable{TSource},Func{TSource,ValueTuple{string,TInput}},Func{TSource,string},Action{TSource,ManifestOptions},string,string,int)"/>.
    /// The <paramref name="name"/> automatically derives <c>groupId</c>, <c>prunePrefix</c>, and
    /// the external ID prefix — reducing boilerplate when the naming follows the <c>{name}-{suffix}</c> pattern.
    /// Each item explicitly maps to its parent via the <paramref name="dependsOn"/> function.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection</typeparam>
    /// <param name="name">The batch name. Used as <c>groupId</c>, <c>prunePrefix</c> is <c>"{name}-"</c>, and each external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="sources">The collection of source items to create dependent manifests from</param>
    /// <param name="map">A function that transforms each source item into a <c>Suffix</c> and <c>Input</c> pair. The full external ID is <c>"{name}-{suffix}"</c>.</param>
    /// <param name="dependsOn">A function that maps each source item to the external ID of its parent manifest</param>
    /// <param name="configure">Optional action to configure additional manifest options per source item</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Per-item configure callback can override.</param>
    /// <returns>The builder for method chaining</returns>
    /// <example>
    /// <code>
    /// scheduler
    ///     .ScheduleMany&lt;IExtractWorkflow, ExtractInput, int&gt;("extract", ...)
    ///     .IncludeMany&lt;ITransformWorkflow, TransformInput, int&gt;("transform",
    ///         Enumerable.Range(0, 10),
    ///         i => ($"{i}", new TransformInput { Index = i }),
    ///         dependsOn: i => $"extract-{i}")
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
        string name,
        IEnumerable<TSource> sources,
        Func<TSource, (string Suffix, TInput Input)> map,
        Func<TSource, string> dependsOn,
        Action<TSource, ManifestOptions>? configure = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties =>
        IncludeMany<TWorkflow, TInput, TSource>(
            sources,
            source =>
            {
                var (suffix, input) = map(source);
                return ($"{name}-{suffix}", input);
            },
            dependsOn,
            configure,
            prunePrefix: $"{name}-",
            groupId: name,
            priority: priority
        );
}
