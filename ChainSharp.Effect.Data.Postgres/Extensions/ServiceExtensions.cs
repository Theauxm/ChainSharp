using ChainSharp.Effect.Data.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContext;
using ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Data.Postgres.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpLoggingConfigurationBuilder UsePostgresProvider(
        this ChainSharpLoggingConfigurationBuilder configurationBuilder,
        string connectionString
    )
    {
        DatabaseMigrator.Migrate(connectionString).Wait();

        var dataSource = ModelBuilderExtensions.BuildDataSource(connectionString);
        var postgresConnectionFactory = new PostgresContextFactory(dataSource);

        configurationBuilder
            .ServiceCollection.AddSingleton<IDataContextFactory>(postgresConnectionFactory)
            .AddDbContext<IDataContext, PostgresContext>(options =>
            {
                // builder.ConfigureWarnings(configurationBuilder => configurationBuilder
                // .Ignore([CoreEventId.ManyServiceProvidersCreatedWarning]));
                options.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();
            });

        return configurationBuilder;
    }
}
