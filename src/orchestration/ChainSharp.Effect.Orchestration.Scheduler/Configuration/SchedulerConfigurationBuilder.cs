using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestPollingService;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;
using ChainSharp.Effect.Orchestration.Scheduler.Services.MetadataCleanupPollingService;
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
        Action<ManifestOptions>? configure = null
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
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
                        ct
                    )
            }
        );

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
        string? groupId = null
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        // Materialize the sources to avoid multiple enumeration
        var sourceList = sources.ToList();
        var firstId = sourceList.Select(s => map(s).ExternalId).FirstOrDefault() ?? "batch";

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
                        ct: ct
                    );
                    return results.FirstOrDefault()!;
                }
            }
        );

        return this;
    }

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
}
