using ChainSharp.ArrayLogger.Services.ArrayLoggingProvider;
using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Json.Extensions;
using ChainSharp.Effect.Parameter.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.Effect.Data.Postgres.Integration;

[TestFixture]
public abstract class TestSetup
{
    private ServiceProvider ServiceProvider { get; set; }

    public IServiceScope Scope { get; private set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var connectionString = configuration.GetRequiredSection("Configuration")[
            "DatabaseConnectionString"
        ]!;

        var arrayLoggingProvider = new ArrayLoggingProvider();

        var serviceCollection = new ServiceCollection()
            .AddSingleton<ILoggerProvider>(arrayLoggingProvider)
            .AddSingleton<IArrayLoggingProvider>(arrayLoggingProvider)
            .AddLogging()
            .AddChainSharpEffects(
                options =>
                    options
                        .SaveWorkflowParameters()
                        .AddPostgresEffect(connectionString)
                        .AddEffectDataContextLogging(minimumLogLevel: LogLevel.Trace)
                        .AddJsonEffect()
            );

        ServiceProvider = ConfigureServices(serviceCollection).BuildServiceProvider();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await ServiceProvider.DisposeAsync();
    }

    public abstract IServiceCollection ConfigureServices(IServiceCollection services);

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
