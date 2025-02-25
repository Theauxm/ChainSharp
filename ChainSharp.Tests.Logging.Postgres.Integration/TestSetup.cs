using ChainSharp.Logging.Postgres.Extensions;
using ChainSharp.Logging.Postgres.Services.PostgresContext;
using ChainSharp.Logging.Postgres.Services.PostgresContextFactory;
using ChainSharp.Logging.Postgres.Utils;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ChainSharp.Tests.Logging.Postgres.Integration;

public abstract class TestSetup
{
    private ServiceProvider ServiceProvider { get; set; }

    public IServiceScope Scope { get; private set; }

    private ServiceCollection ServiceCollection { get; set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        ServiceCollection = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var connectionString = configuration.GetRequiredSection("Configuration")[
            "DatabaseConnectionString"
        ]!;

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await connection.ReloadTypesAsync();

        var command = new NpgsqlCommand("create schema if not exists chain_sharp;", connection);
        await command.ExecuteNonQueryAsync();

        var result = DatabaseMigrator
            .CreateEngineWithEmbeddedScripts(connectionString)
            .PerformUpgrade();

        var dataSource = ModelBuilderExtensions.BuildDataSource(connectionString);
        var postgresConnectionFactory = new PostgresContextFactory(dataSource);

        ServiceCollection.AddSingleton<ILoggingProviderContextFactory>(postgresConnectionFactory);

        ServiceProvider = ConfigureServices(ServiceCollection);
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await ServiceProvider.DisposeAsync();
    }

    public abstract ServiceProvider ConfigureServices(IServiceCollection services);

    [SetUp]
    public virtual async Task TestSetUp()
    {
        Scope = ServiceProvider.CreateScope();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        Scope.Dispose();
    }
}
