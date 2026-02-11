using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
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
            // Configure Hangfire with PostgreSQL storage and disable automatic retries
            // ChainSharp.Effect.Orchestration.Scheduler manages retries through its own manifest system
            services.AddHangfire(
                config =>
                    config
                        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                        .UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString))
                        .UseFilter(new AutomaticRetryAttribute { Attempts = 0 })
            );

            // Add Hangfire server with default settings
            services.AddHangfireServer(options =>
            {
                options.Queues = ["default", "scheduler"];
            });

            // Register HangfireTaskServer as IBackgroundTaskServer
            services.AddSingleton<IBackgroundTaskServer, HangfireTaskServer>();
        });

        return builder;
    }
}
