using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Hangfire.Filters;
using ChainSharp.Effect.Orchestration.Scheduler.Hangfire.Services.HangfireTaskServer;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Orchestration.Scheduler.Hangfire.Extensions;

/// <summary>
/// Extension methods for configuring Hangfire as the background task server for ChainSharp.Effect.Orchestration.Scheduler.
/// </summary>
public static class HangfireServiceExtensions
{
    /// <summary>
    /// Configures Hangfire as the background task server for the scheduler using PostgreSQL storage.
    /// </summary>
    /// <param name="builder">The scheduler configuration builder</param>
    /// <param name="connectionString">PostgreSQL connection string for Hangfire storage</param>
    /// <returns>The scheduler configuration builder for continued chaining</returns>
    /// <remarks>
    /// Hangfire is configured internally with sensible defaults. Retries are disabled since
    /// ChainSharp.Effect.Orchestration.Scheduler manages its own retry logic through the manifest system.
    ///
    /// <code>
    /// services.AddChainSharpEffects(options => options
    ///     .AddEffectWorkflowBus(assemblies)
    ///     .AddPostgresEffect(connectionString)
    ///     .AddScheduler(scheduler => scheduler
    ///         .PollingInterval(TimeSpan.FromSeconds(30))
    ///         .UseHangfire(connectionString)
    ///     )
    /// );
    /// </code>
    /// </remarks>
    public static SchedulerConfigurationBuilder UseHangfire(
        this SchedulerConfigurationBuilder builder,
        string connectionString
    )
    {
        builder.UseTaskServer(services =>
        {
            // Configure Hangfire with PostgreSQL storage.
            // Retries and job lifecycle are fully managed by ChainSharp's Scheduler,
            // so we disable Hangfire's automatic retries and auto-delete completed jobs.
            services.AddHangfire(
                config =>
                    config
                        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                        .UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UsePostgreSqlStorage(
                            opts => opts.UseNpgsqlConnection(connectionString),
                            new PostgreSqlStorageOptions
                            {
                                // Set above DefaultJobTimeout (20m) to prevent Hangfire from
                                // re-enqueueing long-running jobs that ChainSharp is still tracking.
                                InvisibilityTimeout = TimeSpan.FromMinutes(30)
                            }
                        )
                        .UseFilter(new AutomaticRetryAttribute { Attempts = 0 })
                        .UseFilter(new AutoDeleteOnSuccessFilter())
            );

            services.AddHangfireServer(options =>
            {
                options.Queues = ["default", "scheduler"];

                // Allow slightly more than DefaultJobTimeout for Hangfire's own
                // server heartbeat before it considers the server dead.
                options.ServerTimeout = TimeSpan.FromMinutes(25);

                // Give in-flight jobs time to finish gracefully on shutdown.
                options.ShutdownTimeout = TimeSpan.FromSeconds(30);
            });

            // Register HangfireTaskServer as IBackgroundTaskServer
            services.AddSingleton<IBackgroundTaskServer, HangfireTaskServer>();
        });

        return builder;
    }
}
