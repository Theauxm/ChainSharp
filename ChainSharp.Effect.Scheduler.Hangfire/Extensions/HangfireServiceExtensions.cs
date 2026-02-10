using ChainSharp.Effect.Scheduler.Configuration;
using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Scheduler.Hangfire.Extensions;

/// <summary>
/// Extension methods for configuring Hangfire as the background task server for ChainSharp.Effect.Scheduler.
/// </summary>
public static class HangfireServiceExtensions
{
    /// <summary>
    /// Adds Hangfire as the background task server for ChainSharp.Effect.Scheduler.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureHangfire">Action to configure Hangfire (e.g., storage provider)</param>
    /// <param name="configureServer">Optional action to configure the Hangfire server options</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers:
    /// - Hangfire services with the provided configuration
    /// - <see cref="HangfireTaskServer"/> as the <see cref="IBackgroundTaskServer"/> implementation
    ///
    /// You must call <see cref="AddChainSharpScheduler"/> before calling this method.
    ///
    /// Example usage:
    /// ```csharp
    /// services.AddChainSharpScheduler(options =>
    /// {
    ///     options.PollingInterval = TimeSpan.FromSeconds(30);
    /// });
    ///
    /// services.AddHangfireTaskServer(
    ///     config => config.UseSqlServerStorage(connectionString),
    ///     server => server.WorkerCount = 4
    /// );
    /// ```
    /// </remarks>
    public static IServiceCollection AddHangfireTaskServer(
        this IServiceCollection services,
        Action<IGlobalConfiguration> configureHangfire,
        Action<BackgroundJobServerOptions>? configureServer = null
    )
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
        // Singleton is appropriate here since HangfireTaskServer only wraps 
        // IBackgroundJobClient and IRecurringJobManager, which are singletons
        services.AddSingleton<IBackgroundTaskServer, Services.HangfireTaskServer.HangfireTaskServer>();

        return services;
    }

    /// <summary>
    /// Starts the recurring manifest polling job.
    /// </summary>
    /// <param name="backgroundTaskServer">The background task server</param>
    /// <param name="schedulerConfiguration">The scheduler configuration</param>
    /// <param name="recurringJobId">Optional custom recurring job ID (default: "manifest-manager-poll")</param>
    /// <remarks>
    /// Call this method during application startup (e.g., in Program.cs after app.Build())
    /// to set up the recurring job that triggers manifest processing.
    ///
    /// Example usage:
    /// ```csharp
    /// var app = builder.Build();
    ///
    /// // Start the manifest polling
    /// var taskServer = app.Services.GetRequiredService&lt;IBackgroundTaskServer&gt;();
    /// var config = app.Services.GetRequiredService&lt;SchedulerConfiguration&gt;();
    /// taskServer.StartManifestPolling(config);
    ///
    /// app.Run();
    /// ```
    /// </remarks>
    public static void StartManifestPolling(
        this IBackgroundTaskServer backgroundTaskServer,
        SchedulerConfiguration schedulerConfiguration,
        string recurringJobId = "manifest-manager-poll"
    )
    {
        // Convert TimeSpan to cron expression
        // For intervals < 1 hour, use minute-based cron
        // For intervals >= 1 hour, use hour-based cron
        var cronExpression = ToCronExpression(schedulerConfiguration.PollingInterval);

        backgroundTaskServer.AddOrUpdateRecurringManifestPoll(recurringJobId, cronExpression);
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
