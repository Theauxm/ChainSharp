using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Data.Postgres.Integration;

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

        ServiceCollection.AddChainSharpEffects(
            options => options.AddPostgresEffect(connectionString).AddConsoleLogger()
        );

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
