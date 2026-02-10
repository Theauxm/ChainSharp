using ChainSharp.Effect.Scheduler.Configuration;
using ChainSharp.Effect.Scheduler.Hangfire.Services.HangfireTaskServer;
using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Scheduler.Hangfire.Extensions;

/// <summary>
/// Extension methods for configuring Hangfire as the background task server for ChainSharp.Effect.Scheduler.
/// </summary>
public static class HangfireServiceExtensions
{
    /// <summary>
    /// Configures Hangfire as the background task server for the scheduler.
    /// </summary>
    /// <param name="builder">The scheduler configuration builder</param>
    /// <param name="configureHangfire">Action to configure Hangfire (e.g., storage provider)</param>
    /// <param name="configureServer">Optional action to configure the Hangfire server options</param>
    /// <returns>The scheduler configuration builder for continued chaining</returns>
    /// <remarks>
    /// Use this method within the AddScheduler configuration to set up Hangfire:
    ///
    /// <code>
    /// services.AddChainSharpEffects(options => options
    ///     .AddEffectWorkflowBus(assemblies)
    ///     .AddPostgresEffect(connectionString)
    ///     .AddScheduler(scheduler => scheduler
    ///         .PollingInterval(TimeSpan.FromSeconds(30))
    ///         .UseHangfire(
    ///             config => config
    ///                 .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    ///                 .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)),
    ///             server => server.WorkerCount = Environment.ProcessorCount * 2
    ///         )
    ///     )
    /// );
    /// </code>
    /// </remarks>
    public static SchedulerConfigurationBuilder UseHangfire(
        this SchedulerConfigurationBuilder builder,
        Action<IGlobalConfiguration> configureHangfire,
        Action<BackgroundJobServerOptions>? configureServer = null
    )
    {
        builder.UseTaskServer(services =>
        {
            // Configure Hangfire
            services.AddHangfire(configureHangfire);

            // Configure and add Hangfire server
            var serverOptions = new BackgroundJobServerOptions();
            configureServer?.Invoke(serverOptions);
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = serverOptions.WorkerCount;
                options.Queues = serverOptions.Queues;
                options.ServerName = serverOptions.ServerName;
                options.ShutdownTimeout = serverOptions.ShutdownTimeout;
                options.SchedulePollingInterval = serverOptions.SchedulePollingInterval;
            });

            // Register HangfireTaskServer as IBackgroundTaskServer
            services.AddSingleton<IBackgroundTaskServer, HangfireTaskServer>();
        });

        return builder;
    }

    /// <summary>
    /// Enables ChainSharp scheduler and starts the recurring manifest polling job.
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
    /// </remarks>
    public static IApplicationBuilder UseChainSharpScheduler(
        this IApplicationBuilder app,
        string recurringJobId = "manifest-manager-poll"
    )
    {
        var taskServer = app.ApplicationServices.GetRequiredService<IBackgroundTaskServer>();
        var config = app.ApplicationServices.GetRequiredService<SchedulerConfiguration>();

        var cronExpression = ToCronExpression(config.PollingInterval);
        taskServer.AddOrUpdateRecurringManifestPoll(recurringJobId, cronExpression);

        return app;
    }

    /// <summary>
    /// Converts a TimeSpan to a cron expression.
    /// </summary>
    /// <param name="interval">The interval to convert</param>
    /// <returns>A cron expression representing the interval</returns>
    private static string ToCronExpression(TimeSpan interval)
    {
        // Round to nearest minute
        var totalMinutes = (int)Math.Max(1, Math.Round(interval.TotalMinutes));

        return totalMinutes switch
        {
            1 => "* * * * *", // Every minute
            < 60 when 60 % totalMinutes == 0 => $"*/{totalMinutes} * * * *", // Every N minutes
            60 => "0 * * * *", // Every hour
            > 60 when totalMinutes % 60 == 0 && 24 % (totalMinutes / 60) == 0
                => $"0 */{totalMinutes / 60} * * *", // Every N hours
            _ => $"*/{Math.Min(totalMinutes, 59)} * * * *" // Fallback to closest minute interval
        };
    }
}
