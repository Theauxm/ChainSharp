using System.Collections.Concurrent;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.Enums;
using ChainSharp.Effect.Data.Services.DataContextLoggingProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddEffectLogging(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
        EvaluationStrategy? evaluationStrategy = null,
        LogLevel? minimumLogLevel = null,
        List<string>? blacklist = null
    )
    {
        var logLevelEnvironment = Environment.GetEnvironmentVariable(
            "CHAIN_SHARP_POSTGRES_LOG_LEVEL"
        );

        var parsed = Enum.TryParse<LogLevel>(logLevelEnvironment, out var logLevel);

        if (parsed)
            minimumLogLevel ??= logLevel;

        if (configurationBuilder.LoggingEffectEnabled == false)
            throw new Exception(
                "Logging effect is not enabled in ChainSharp. Call .AddChainSharpEffects(x => x.AddPostgresEffect(connectionString).AddEffectLogging())"
            );

        var credentials = new DataContextLoggingProviderConfiguration
        {
            EvaluationStrategy = evaluationStrategy ?? EvaluationStrategy.Eager,
            MinimumLogLevel = minimumLogLevel ?? LogLevel.Information,
            Blacklist = blacklist ?? []
        };

        configurationBuilder
            .ServiceCollection.AddSingleton<IDataContextLoggingProviderConfiguration>(credentials)
            .AddSingleton(
                new BlockingCollection<Effect.Models.Log.Log>(
                    new ConcurrentQueue<Effect.Models.Log.Log>()
                )
            )
            .AddSingleton<ILoggerProvider, DataContextLoggingProvider>();

        return configurationBuilder;
    }
}
