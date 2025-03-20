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
        EvaluationStrategy evaluationStrategy
    )
    {
        if (configurationBuilder.PostgresEffectsEnabled == false)
            throw new Exception(
                "Postgres effect is not enabled in ChainSharp. Call .AddChainSharpEffects(x => x.AddPostgresEffect(connectionString).AddPostgresEffectLogging())"
            );

        var credentials = new DataContextLoggingProviderCredentials()
        {
            EvaluationStrategy = evaluationStrategy
        };

        configurationBuilder
            .ServiceCollection.AddSingleton<IDataContextLoggingProviderCredentials>(credentials)
            .AddSingleton<ILoggerProvider, DataContextLoggingProvider>();

        return configurationBuilder;
    }
}
