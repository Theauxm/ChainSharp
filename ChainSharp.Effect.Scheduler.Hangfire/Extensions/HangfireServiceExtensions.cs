using ChainSharp.Effect.Scheduler.Configuration;
using ChainSharp.Effect.Scheduler.Hangfire.Services.HangfireTaskServer;
using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Scheduler.Services.ManifestScheduler;
using ChainSharp.Effect.Scheduler.Services.Scheduling;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Scheduler.Hangfire.Extensions;

/// <summary>
/// Extension methods for configuring Hangfire as the background task server for ChainSharp.Effect.Scheduler.
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
    /// ChainSharp.Effect.Scheduler manages its own retry logic through the manifest system.
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
            // ChainSharp.Effect.Scheduler manages retries through its own manifest system
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

    /// <summary>
    /// Enables ChainSharp scheduler, seeds pending manifests, and starts the recurring manifest polling job.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="recurringJobId">Optional custom recurring job ID (default: "manifest-manager-poll")</param>
    /// <returns>The application builder for method chaining</returns>
    /// <remarks>
    /// Call this method during application startup after building the app.
    /// This is typically called alongside UseHangfireDashboard:
    ///
    /// <code>
    /// var app = builder.Build();
    ///
    /// app.UseHangfireDashboard("/hangfire");
    /// app.UseChainSharpScheduler();
    ///
    /// app.Run();
    /// </code>
    ///
    /// This method also processes any pending manifests that were scheduled during
    /// configuration via the <c>.Schedule()</c> and <c>.ScheduleMany()</c> methods.
    /// </remarks>
    public static IApplicationBuilder UseChainSharpScheduler(
        this IApplicationBuilder app,
        string recurringJobId = "manifest-manager-poll"
    )
    {
        var taskServer = app.ApplicationServices.GetRequiredService<IBackgroundTaskServer>();
        var config = app.ApplicationServices.GetRequiredService<SchedulerConfiguration>();
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("ChainSharp.Scheduler");

        // Seed pending manifests from configuration
        SeedPendingManifests(app.ApplicationServices, config, logger);

        // Start the recurring manifest polling job
        var cronExpression = Schedule.FromInterval(config.PollingInterval).ToCronExpression();
        taskServer.AddOrUpdateRecurringManifestPoll(recurringJobId, cronExpression);

        return app;
    }

    /// <summary>
    /// Seeds pending manifests that were configured during startup.
    /// </summary>
    private static void SeedPendingManifests(
        IServiceProvider services,
        SchedulerConfiguration config,
        ILogger? logger
    )
    {
        if (config.PendingManifests.Count == 0)
            return;

        logger?.LogInformation(
            "Seeding {Count} pending manifest(s) from startup configuration...",
            config.PendingManifests.Count
        );

        using var scope = services.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IManifestScheduler>();

        foreach (var pending in config.PendingManifests)
        {
            try
            {
                // The closure already has the generic types captured - just invoke it
                pending.ScheduleFunc(scheduler, CancellationToken.None).GetAwaiter().GetResult();
                logger?.LogDebug("Seeded manifest: {ExternalId}", pending.ExternalId);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "Failed to seed manifest {ExternalId}: {Message}",
                    pending.ExternalId,
                    ex.Message
                );
                throw;
            }
        }

        logger?.LogInformation(
            "Successfully seeded {Count} manifest(s)",
            config.PendingManifests.Count
        );
    }
}
