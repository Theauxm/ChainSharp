using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.Enums;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContext;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.DataContextLoggingProvider;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddPostgresEffect(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
        string connectionString
    )
    {
        DatabaseMigrator.Migrate(connectionString).Wait();

        configurationBuilder
            .ServiceCollection.AddSingleton(
                _ => ModelBuilderExtensions.BuildDataSource(connectionString)
            )
            .AddDbContextFactory<PostgresContext>(
                (sp, options) =>
                {
                    var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
                    options.UseNpgsql(dataSource);
                }
            );

        configurationBuilder.LoggingEffectEnabled = true;

        return configurationBuilder.AddEffect<
            IDataContextProviderFactory,
            PostgresContextProviderFactory
        >();
    }
}
