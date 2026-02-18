using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestPollingService;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;
using ChainSharp.Effect.Orchestration.Scheduler.Services.MetadataCleanupPollingService;
using ChainSharp.Effect.Orchestration.Scheduler.Utilities;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Schedule = ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Orchestration.Scheduler.Configuration;

/// <summary>
/// Fluent builder for configuring the ChainSharp scheduler.
/// </summary>
/// <remarks>
/// This builder allows configuring the scheduler as part of the ChainSharp effects setup:
/// <code>
/// services.AddChainSharpEffects(options => options
///     .AddEffectWorkflowBus(assemblies)
///     .AddPostgresEffect(connectionString)
///     .AddScheduler(scheduler => scheduler
///         .PollingInterval(TimeSpan.FromSeconds(30))
///         .MaxActiveJobs(100)
///         .UseHangfire(config => config.UsePostgreSqlStorage(...))
///     )
/// );
/// </code>
/// </remarks>
public class SchedulerConfigurationBuilder
{
    private readonly ChainSharpEffectConfigurationBuilder _parentBuilder;
    private readonly SchedulerConfiguration _configuration = new();
    private Action<IServiceCollection>? _taskServerRegistration;
    private string? _rootScheduledExternalId;
    private string? _lastScheduledExternalId;

    // Dependency graph tracking for cycle detection at build time
    private readonly Dictionary<string, string> _externalIdToGroupId = new();
    private readonly List<(string ParentExternalId, string ChildExternalId)> _dependencyEdges = [];

    /// <summary>
    /// Creates a new scheduler configuration builder.
    /// </summary>
    /// <param name="parentBuilder">The parent ChainSharp effect configuration builder</param>
    public SchedulerConfigurationBuilder(ChainSharpEffectConfigurationBuilder parentBuilder)
    {
        _parentBuilder = parentBuilder;
    }

    /// <summary>
    /// Gets the service collection for registering services.
    /// </summary>
    public IServiceCollection ServiceCollection => _parentBuilder.ServiceCollection;

    /// <summary>
    /// Sets the interval at which the ManifestManager polls for pending jobs.
    /// </summary>
    /// <param name="interval">The polling interval (default: 60 seconds)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder PollingInterval(TimeSpan interval)
    {
        _configuration.PollingInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of active jobs (Pending + InProgress) allowed across all manifests.
    /// </summary>
    /// <param name="maxJobs">The maximum active jobs (default: 100, null = unlimited)</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// When the total number of active jobs reaches this limit, no new jobs will be enqueued
    /// until existing jobs complete.
    /// </remarks>
    public SchedulerConfigurationBuilder MaxActiveJobs(int? maxJobs)
    {
        _configuration.MaxActiveJobs = maxJobs;
        return this;
    }

    /// <summary>
    /// Excludes a workflow type from the MaxActiveJobs count.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type to exclude</typeparam>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// Internal scheduler workflows are excluded by default. Use this method to
    /// exclude additional workflow types whose Metadata should not count toward the limit.
    /// </remarks>
    public SchedulerConfigurationBuilder ExcludeFromMaxActiveJobs<TWorkflow>()
        where TWorkflow : class
    {
        _configuration.ExcludedWorkflowTypeNames.Add(typeof(TWorkflow).FullName!);
        return this;
    }

    /// <summary>
    /// Sets the priority boost automatically applied to dependent workflow work queue entries.
    /// </summary>
    /// <param name="boost">The priority boost (default: 16, range: 0-31)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder DependentPriorityBoost(int boost)
    {
        _configuration.DependentPriorityBoost = Math.Clamp(
            boost,
            WorkQueue.MinPriority,
            WorkQueue.MaxPriority
        );
        return this;
    }

    /// <summary>
    /// Sets the default number of retry attempts before a job is dead-lettered.
    /// </summary>
    /// <param name="maxRetries">The maximum retry count (default: 3)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder DefaultMaxRetries(int maxRetries)
    {
        _configuration.DefaultMaxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Sets the default delay between retry attempts.
    /// </summary>
    /// <param name="delay">The retry delay (default: 5 minutes)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder DefaultRetryDelay(TimeSpan delay)
    {
        _configuration.DefaultRetryDelay = delay;
        return this;
    }

    /// <summary>
    /// Sets the multiplier applied to retry delay on each subsequent retry.
    /// </summary>
    /// <param name="multiplier">The backoff multiplier (default: 2.0)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder RetryBackoffMultiplier(double multiplier)
    {
        _configuration.RetryBackoffMultiplier = multiplier;
        return this;
    }

    /// <summary>
    /// Sets the maximum retry delay to prevent unbounded backoff growth.
    /// </summary>
    /// <param name="maxDelay">The maximum delay (default: 1 hour)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder MaxRetryDelay(TimeSpan maxDelay)
    {
        _configuration.MaxRetryDelay = maxDelay;
        return this;
    }

    /// <summary>
    /// Sets the timeout after which a running job is considered stuck.
    /// </summary>
    /// <param name="timeout">The job timeout (default: 1 hour)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder DefaultJobTimeout(TimeSpan timeout)
    {
        _configuration.DefaultJobTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets whether to automatically recover stuck jobs on scheduler startup.
    /// </summary>
    /// <param name="recover">True to recover stuck jobs (default: true)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder RecoverStuckJobsOnStartup(bool recover = true)
    {
        _configuration.RecoverStuckJobsOnStartup = recover;
        return this;
    }

    /// <summary>
    /// Uses the in-memory task server for testing and development.
    /// </summary>
    /// <remarks>
    /// The in-memory task server executes jobs immediately and synchronously.
    /// Useful for unit/integration testing without external infrastructure.
    /// </remarks>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder UseInMemoryTaskServer()
    {
        _taskServerRegistration = services =>
        {
            services.AddScoped<IBackgroundTaskServer, InMemoryTaskServer>();
        };
        return this;
    }

    /// <summary>
    /// Registers a custom background task server registration action.
    /// </summary>
    /// <remarks>
    /// This is used by task server implementations (Hangfire, Quartz, etc.) to register
    /// their services. Most users should use the specific extension methods like UseHangfire().
    /// </remarks>
    /// <param name="registration">The action to register task server services</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder UseTaskServer(Action<IServiceCollection> registration)
    {
        _taskServerRegistration = registration;
        return this;
    }

    /// <summary>
    /// Schedules a workflow to run on a recurring basis.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <param name="externalId">A unique identifier for this scheduled job</param>
    /// <param name="input">The input data that will be passed to the workflow on each execution</param>
    /// <param name="schedule">The schedule definition (interval or cron-based)</param>
    /// <param name="configure">Optional action to configure additional manifest options</param>
    /// <param name="priority">The dispatch priority (0-31, default 0). Higher values are dispatched first. Can be overridden by the configure callback.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// The manifest is not created immediately. It is captured and will be seeded
    /// automatically on startup by the ManifestPollingService.
    /// All scheduled manifests use upsert semantics based on ExternalId.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddChainSharpEffects(options => options
    ///     .AddScheduler(scheduler => scheduler
    ///         .UseHangfire(/* ... */)
    ///         .Schedule&lt;IHelloWorldWorkflow, HelloWorldInput&gt;(
    ///             "hello-world",
    ///             new HelloWorldInput { Name = "Scheduler" },
    ///             Every.Minutes(1))
    ///     )
    /// );
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder Schedule<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        _externalIdToGroupId[externalId] = groupId ?? externalId;

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = externalId,
                ScheduleFunc = (scheduler, ct) =>
                    scheduler.ScheduleAsync<TWorkflow, TInput>(
                        externalId,
                        input,
                        schedule,
                        configure,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    )
            }
        );

        _rootScheduledExternalId = externalId;
        _lastScheduledExternalId = externalId;

        return this;
    }

    /// <summary>
    /// Schedules a dependent workflow that runs after the previously scheduled manifest succeeds.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <param name="externalId">A unique identifier for this dependent job</param>
    /// <param name="input">The input data that will be passed to the workflow on each execution</param>
    /// <param name="configure">Optional action to configure additional manifest options</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Can be overridden by the configure callback.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// Must be called after <see cref="Schedule{TWorkflow,TInput}"/>, <see cref="Include{TWorkflow,TInput}"/>,
    /// or another <c>ThenInclude</c> call.
    /// The dependent manifest will be queued when the parent's LastSuccessfulRun is newer than its own.
    /// Supports chaining: <c>.Schedule(...).Include(...).ThenInclude(...)</c> for branched dependency chains.
    /// </remarks>
    public SchedulerConfigurationBuilder ThenInclude<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var parentExternalId =
            _lastScheduledExternalId
            ?? throw new InvalidOperationException(
                "ThenInclude() must be called after Schedule(), Include(), or another ThenInclude(). "
                    + "No parent manifest external ID is available."
            );

        _externalIdToGroupId[externalId] = groupId ?? externalId;
        _dependencyEdges.Add((parentExternalId, externalId));

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = externalId,
                ScheduleFunc = (scheduler, ct) =>
                    scheduler.ScheduleDependentAsync<TWorkflow, TInput>(
                        externalId,
                        input,
                        parentExternalId,
                        configure,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    ),
            }
        );

        _lastScheduledExternalId = externalId;

        return this;
    }

    /// <summary>
    /// Schedules a dependent workflow that runs after the root <see cref="Schedule{TWorkflow,TInput}"/> manifest succeeds.
    /// Unlike <see cref="ThenInclude{TWorkflow,TInput}"/> which chains from the most recent manifest,
    /// <c>Include</c> always branches from the root <c>Schedule</c>, enabling fan-out patterns.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <param name="externalId">A unique identifier for this dependent job</param>
    /// <param name="input">The input data that will be passed to the workflow on each execution</param>
    /// <param name="configure">Optional action to configure additional manifest options</param>
    /// <param name="groupId">Optional group identifier for dashboard grouping</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Can be overridden by the configure callback.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// Must be called after <see cref="Schedule{TWorkflow,TInput}"/>.
    /// Use <c>Include</c> to create multiple independent branches from a single root:
    /// <code>
    /// .Schedule&lt;A&gt;(...)           // root=A
    ///     .Include&lt;B&gt;(...)        // B depends on A (root)
    ///     .Include&lt;C&gt;(...)        // C depends on A (root)
    ///         .ThenInclude&lt;D&gt;(...)       // D depends on C (cursor)
    /// </code>
    /// Result: A → B, A → C → D
    /// </remarks>
    public SchedulerConfigurationBuilder Include<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var parentExternalId =
            _rootScheduledExternalId
            ?? throw new InvalidOperationException(
                "Include() must be called after Schedule(). "
                    + "No root manifest external ID is available."
            );

        _externalIdToGroupId[externalId] = groupId ?? externalId;
        _dependencyEdges.Add((parentExternalId, externalId));

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = externalId,
                ScheduleFunc = (scheduler, ct) =>
                    scheduler.ScheduleDependentAsync<TWorkflow, TInput>(
                        externalId,
                        input,
                        parentExternalId,
                        configure,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    ),
            }
        );

        _lastScheduledExternalId = externalId;

        return this;
    }

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

    /// <summary>
    /// Enables automatic cleanup of metadata for system and other noisy workflows.
    /// </summary>
    /// <remarks>
    /// By default, metadata from <c>ManifestManagerWorkflow</c> and
    /// <c>MetadataCleanupWorkflow</c> will be cleaned up. Additional workflow types
    /// can be added via the configure action.
    ///
    /// <code>
    /// .AddScheduler(scheduler => scheduler
    ///     .AddMetadataCleanup(cleanup =>
    ///     {
    ///         cleanup.RetentionPeriod = TimeSpan.FromHours(2);
    ///         cleanup.CleanupInterval = TimeSpan.FromMinutes(1);
    ///         cleanup.AddWorkflowType&lt;MyNoisyWorkflow&gt;();
    ///     })
    /// )
    /// </code>
    /// </remarks>
    /// <param name="configure">Optional action to customize cleanup behavior</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder AddMetadataCleanup(
        Action<MetadataCleanupConfiguration>? configure = null
    )
    {
        var config = new MetadataCleanupConfiguration();

        // Add default workflow types whose metadata should be cleaned up
        config.AddWorkflowType<ManifestManagerWorkflow>();
        config.AddWorkflowType<MetadataCleanupWorkflow>();

        configure?.Invoke(config);

        _configuration.MetadataCleanup = config;

        return this;
    }

    /// <summary>
    /// Builds the scheduler configuration and registers all services.
    /// </summary>
    /// <returns>The parent builder for continued chaining</returns>
    internal ChainSharpEffectConfigurationBuilder Build()
    {
        ValidateNoCyclicGroupDependencies();

        // Exclude internal scheduler workflows from MaxActiveJobs count
        foreach (var name in AdminWorkflows.FullNames)
            _configuration.ExcludedWorkflowTypeNames.Add(name);

        // Register the configuration
        _parentBuilder.ServiceCollection.AddSingleton(_configuration);

        // Register IManifestScheduler
        _parentBuilder.ServiceCollection.AddScoped<IManifestScheduler, ManifestScheduler>();

        // Register JobDispatcher workflow (must use AddScopedChainSharpWorkflow for property injection)
        _parentBuilder.ServiceCollection.AddScopedChainSharpWorkflow<
            IJobDispatcherWorkflow,
            JobDispatcherWorkflow
        >();

        // Register task server if configured
        _taskServerRegistration?.Invoke(_parentBuilder.ServiceCollection);

        // Register the background polling service (seeds manifests on startup, then polls)
        _parentBuilder.ServiceCollection.AddHostedService<ManifestPollingService>();

        // Register the metadata cleanup service if configured
        if (_configuration.MetadataCleanup is not null)
            _parentBuilder.ServiceCollection.AddHostedService<MetadataCleanupPollingService>();

        return _parentBuilder;
    }

    /// <summary>
    /// Validates that the manifest group dependency graph is a DAG (no circular dependencies).
    /// </summary>
    private void ValidateNoCyclicGroupDependencies()
    {
        if (_dependencyEdges.Count == 0)
            return;

        // Derive group-level edges from manifest-level edges
        var groupNodes = new System.Collections.Generic.HashSet<string>(
            _externalIdToGroupId.Values
        );
        var groupEdges = _dependencyEdges
            .Select(e =>
            {
                _externalIdToGroupId.TryGetValue(e.ParentExternalId, out var fromGroup);
                _externalIdToGroupId.TryGetValue(e.ChildExternalId, out var toGroup);
                return (From: fromGroup, To: toGroup);
            })
            .Where(e => e.From is not null && e.To is not null && e.From != e.To)
            .Select(e => (e.From!, e.To!))
            .Distinct()
            .ToList();

        if (groupEdges.Count == 0)
            return;

        var result = DagValidator.TopologicalSort(groupNodes, groupEdges);

        if (!result.IsAcyclic)
        {
            var cycleGroups = string.Join(", ", result.CycleMembers.Order());
            throw new InvalidOperationException(
                $"Circular dependency detected among manifest groups: [{cycleGroups}]. "
                    + "Manifest groups must form a directed acyclic graph (DAG)."
            );
        }
    }
}
