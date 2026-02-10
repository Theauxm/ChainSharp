using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Scheduler.Configuration;

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
///         .MaxJobsPerCycle(100)
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
    /// Sets the maximum number of jobs that can be enqueued in a single polling cycle.
    /// </summary>
    /// <param name="maxJobs">The maximum jobs per cycle (default: 100)</param>
    /// <returns>The builder for method chaining</returns>
    public SchedulerConfigurationBuilder MaxJobsPerCycle(int maxJobs)
    {
        _configuration.MaxJobsPerCycle = maxJobs;
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
        _taskServerRegistration = services => { services.AddScoped<IBackgroundTaskServer, InMemoryTaskServer>(); };
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
    /// Builds the scheduler configuration and registers all services.
    /// </summary>
    /// <returns>The parent builder for continued chaining</returns>
    internal ChainSharpEffectConfigurationBuilder Build()
    {
        // Register the configuration
        _parentBuilder.ServiceCollection.AddSingleton(_configuration);

        // Register task server if configured
        _taskServerRegistration?.Invoke(_parentBuilder.ServiceCollection);

        return _parentBuilder;
    }
}
