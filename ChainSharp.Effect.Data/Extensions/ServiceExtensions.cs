using System.Collections.Concurrent;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.Enums;
using ChainSharp.Effect.Data.Services.DataContextLoggingProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddEffectDataContextLogging(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
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

        if (configurationBuilder.DataContextLoggingEffectEnabled == false)
            throw new Exception(
                "Data Context Logging effect is not enabled in ChainSharp. Ensure a Data Effect has been added to ChainSharpEffects (before calling AddEffectDataContextLogging). e.g. .AddChainSharpEffects(x => x.AddPostgresEffect(connectionString).AddDataContextEffectLogging())"
            );

        var credentials = new DataContextLoggingProviderConfiguration
        {
            MinimumLogLevel = minimumLogLevel ?? LogLevel.Information,
            Blacklist = blacklist ?? []
        };

        configurationBuilder
            .ServiceCollection.AddSingleton<IDataContextLoggingProviderConfiguration>(credentials)
            .AddSingleton<ILoggerProvider, DataContextLoggingProvider>();

        return configurationBuilder;
    }
}
