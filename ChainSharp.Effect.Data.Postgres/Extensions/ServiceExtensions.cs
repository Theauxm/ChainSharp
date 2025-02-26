using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContext;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Services.EffectFactory;
using ChainSharp.Effect.Services.EffectLogger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var postgresConnectionFactory = new PostgresContextFactory(dataSource);

        return configurationBuilder.AddEffect<IDataContextFactory, PostgresContextFactory>(
            postgresConnectionFactory
        );
    }
}
