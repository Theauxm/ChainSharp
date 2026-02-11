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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Extensions;

/// <summary>
/// Provides extension methods for configuring ChainSharp.Effect.Data.Postgres services in the dependency injection container.
/// </summary>
/// <remarks>
/// The ServiceExtensions class contains utility methods that simplify the registration
/// of ChainSharp.Effect.Data.Postgres services with the dependency injection system.
///
/// These extensions enable:
/// 1. Easy configuration of PostgreSQL database contexts
/// 2. Automatic database migration
/// 3. Integration with the ChainSharp.Effect configuration system
///
/// By using these extensions, applications can easily configure and use the
/// ChainSharp.Effect.Data.Postgres system with minimal boilerplate code.
/// </remarks>
public static class ServiceExtensions
{
    /// <summary>
    /// Adds PostgreSQL database support to the ChainSharp.Effect system.
    /// </summary>
    /// <param name="configurationBuilder">The ChainSharp effect configuration builder</param>
    /// <param name="connectionString">The connection string to the PostgreSQL database</param>
    /// <returns>The configuration builder for method chaining</returns>
    /// <remarks>
    /// This method configures the ChainSharp.Effect system to use PostgreSQL for workflow metadata persistence.
    /// It performs the following steps:
    ///
    /// 1. Migrates the database schema to the latest version using the DatabaseMigrator
    /// 2. Creates a data source with the necessary enum mappings
    /// 3. Registers a DbContextFactory for creating PostgresContext instances
    /// 4. Enables data context logging
    /// 5. Registers the PostgresContextProviderFactory as an IDataContextProviderFactory
    ///
    /// The PostgreSQL implementation is suitable for production environments where
    /// persistent storage and advanced database features are required.
    ///
    /// Example usage:
    /// ```csharp
    /// services.AddChainSharpEffects(options =>
    ///     options.AddPostgresEffect("Host=localhost;Database=chainsharp;Username=postgres;Password=password")
    /// );
    /// ```
    /// </remarks>
    public static ChainSharpEffectConfigurationBuilder AddPostgresEffect(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
        string connectionString
    )
    {
        // Migrate the database schema to the latest version
        DatabaseMigrator.Migrate(connectionString).Wait();

        // Create a data source with enum mappings
        var dataSource = ModelBuilderExtensions.BuildDataSource(connectionString);

        // Register the DbContextFactory
        configurationBuilder.ServiceCollection.AddDbContextFactory<PostgresContext>(
            (_, options) =>
            {
                options
                    .UseNpgsql(dataSource)
                    .UseLoggerFactory(new NullLoggerFactory())
                    .ConfigureWarnings(x => x.Log(CoreEventId.ManyServiceProvidersCreatedWarning));
            }
        );

        // Register PostgresContext directly for injection (created from the factory)
        configurationBuilder.ServiceCollection.AddScoped<IDataContext, PostgresContext>(
            sp => sp.GetRequiredService<IDbContextFactory<PostgresContext>>().CreateDbContext()
        );

        // Enable data context logging
        configurationBuilder.DataContextLoggingEffectEnabled = true;

        // Register the PostgresContextProviderFactory
        return configurationBuilder.AddEffect<
            IDataContextProviderFactory,
            PostgresContextProviderFactory
        >(toggleable: false);
    }
}
