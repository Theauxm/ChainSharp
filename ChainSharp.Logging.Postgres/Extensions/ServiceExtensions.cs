using ChainSharp.Logging.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Logging.Postgres.Services.PostgresContext;
using ChainSharp.Logging.Postgres.Services.PostgresContextFactory;
using ChainSharp.Logging.Postgres.Utils;
using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Logging.Postgres.Extensions;

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
            .ServiceCollection.AddSingleton<ILoggingProviderContextFactory>(
                postgresConnectionFactory
            )
            .AddDbContext<ILoggingProviderContext, PostgresContext>(options =>
            {
                // builder.ConfigureWarnings(configurationBuilder => configurationBuilder
                // .Ignore([CoreEventId.ManyServiceProvidersCreatedWarning]));
                options.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();
            });

        return configurationBuilder;
    }
}
