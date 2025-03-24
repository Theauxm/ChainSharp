using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.Enums;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Data.Services.DataContextLoggingProvider;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Postgres.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddPostgresEffect(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
        string connectionString
    )
    {
        DatabaseMigrator.Migrate(connectionString).Wait();

        var dataSource = ModelBuilderExtensions.BuildDataSource(connectionString);
        var postgresConnectionFactory = new PostgresContextProviderFactory(dataSource);

        configurationBuilder.PostgresEffectsEnabled = true;

        return configurationBuilder.AddEffect<
            IDataContextProviderFactory,
            PostgresContextProviderFactory
        >(postgresConnectionFactory);
    }

    public static ChainSharpEffectConfigurationBuilder AddPostgresEffectLogging(
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

        if (configurationBuilder.PostgresEffectsEnabled == false)
            throw new Exception(
                "Postgres effect is not enabled in ChainSharp. Call .AddChainSharpEffects(x => x.AddPostgresEffect(connectionString).AddPostgresEffectLogging())"
            );

        var credentials = new DataContextLoggingProviderConfiguration
        {
            EvaluationStrategy = evaluationStrategy ?? EvaluationStrategy.Eager,
            MinimumLogLevel = minimumLogLevel ?? LogLevel.Information,
            Blacklist = blacklist ?? []
        };

        configurationBuilder
            .ServiceCollection.AddSingleton<IDataContextLoggingProviderConfiguration>(credentials)
            .AddSingleton<ILoggerProvider, DataContextLoggingProvider>();

        return configurationBuilder;
    }
}
