using ChainSharp.Effect.Scheduler.Configuration;
using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Scheduler.Services.DeadLetterService;
using ChainSharp.Effect.Scheduler.Services.ManifestManager;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Scheduler.Extensions;

/// <summary>
/// Extension methods for configuring ChainSharp.Effect.Scheduler services.
/// </summary>
public static class SchedulerExtensions
{
    /// <summary>
    /// Adds ChainSharp.Effect.Scheduler services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// This method registers the core scheduler services but does NOT register
    /// a background task server implementation. You must also call one of:
    /// - AddHangfireTaskServer() from ChainSharp.Effect.Scheduler.Hangfire
    /// - AddQuartzTaskServer() from ChainSharp.Effect.Scheduler.Quartz
    /// - Or provide your own IBackgroundTaskServer implementation
    ///
    /// Example usage:
    /// ```csharp
    /// services.AddChainSharpScheduler(options =>
    /// {
    ///     options.PollingInterval = TimeSpan.FromSeconds(30);
    ///     options.DefaultMaxRetries = 5;
    /// });
    ///
    /// // Then add your preferred background task server
    /// services.AddHangfireTaskServer(config => config.UseSqlServerStorage(connectionString));
    /// ```
    /// </remarks>
    public static IServiceCollection AddChainSharpScheduler(
        this IServiceCollection services,
        Action<SchedulerConfiguration>? configure = null
    )
    {
        var configuration = new SchedulerConfiguration();
        configure?.Invoke(configuration);

        services.AddSingleton(configuration);

        // Register other services - implementations will be added later
        // services.AddScoped<IManifestManager, ManifestManager>();
        // services.AddScoped<IDeadLetterService, DeadLetterService>();

        return services;
    }

    /// <summary>
    /// Registers a custom background task server implementation.
    /// </summary>
    /// <typeparam name="TServer">The type of the background task server implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBackgroundTaskServer<TServer>(
        this IServiceCollection services
    )
        where TServer : class, IBackgroundTaskServer
    {
        services.AddScoped<IBackgroundTaskServer, TServer>();
        return services;
    }

    /// <summary>
    /// Registers the in-memory background task server for testing and development.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// The in-memory task server executes jobs immediately and synchronously.
    /// It's useful for:
    /// - Unit and integration testing without Hangfire/Quartz infrastructure
    /// - Local development scenarios
    /// - Simple use cases where true background processing isn't needed
    ///
    /// Example usage:
    /// ```csharp
    /// services.AddChainSharpScheduler();
    /// services.AddInMemoryTaskServer();
    /// ```
    /// </remarks>
    public static IServiceCollection AddInMemoryTaskServer(this IServiceCollection services)
    {
        services.AddScoped<IBackgroundTaskServer, InMemoryTaskServer>();
        return services;
    }
}
