using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;

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

        return configurationBuilder.AddEffect<
            IDataContextProviderFactory,
            PostgresContextProviderFactory
        >(postgresConnectionFactory);
    }
}
