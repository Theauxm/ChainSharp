using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Scheduler.Configuration;

namespace ChainSharp.Effect.Scheduler.Extensions;

/// <summary>
/// Extension methods for configuring ChainSharp.Effect.Scheduler services.
/// </summary>
public static class SchedulerExtensions
{
    /// <summary>
    /// Adds the ChainSharp scheduler as part of the effects configuration.
    /// </summary>
    /// <param name="builder">The ChainSharp effect configuration builder</param>
    /// <param name="configure">Action to configure the scheduler</param>
    /// <returns>The effect configuration builder for continued chaining</returns>
    /// <remarks>
    /// Configure the scheduler within the ChainSharp effects setup:
    ///
    /// <code>
    /// services.AddChainSharpEffects(options => options
    ///     .AddEffectWorkflowBus(assemblies)
    ///     .AddPostgresEffect(connectionString)
    ///     .AddScheduler(scheduler => scheduler
    ///         .PollingInterval(TimeSpan.FromSeconds(30))
    ///         .MaxJobsPerCycle(100)
    ///         .DefaultMaxRetries(5)
    ///         .UseHangfire(
    ///             config => config.UsePostgreSqlStorage(...),
    ///             server => server.WorkerCount = 4
    ///         )
    ///     )
    /// );
    /// </code>
    /// </remarks>
    public static ChainSharpEffectConfigurationBuilder AddScheduler(
        this ChainSharpEffectConfigurationBuilder builder,
        Action<SchedulerConfigurationBuilder> configure
    )
    {
        var schedulerBuilder = new SchedulerConfigurationBuilder(builder);
        configure(schedulerBuilder);
        return schedulerBuilder.Build();
    }
}
