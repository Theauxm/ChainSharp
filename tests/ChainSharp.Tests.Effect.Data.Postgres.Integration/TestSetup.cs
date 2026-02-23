using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Effect.StepProvider.Logging.Extensions;
using ChainSharp.Tests.ArrayLogger.Services.ArrayLoggingProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.Effect.Data.Postgres.Integration;

[TestFixture]
public abstract class TestSetup
{
    private ServiceProvider ServiceProvider { get; set; }

    public IServiceScope Scope { get; private set; }

    public IWorkflowBus WorkflowBus { get; private set; }

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

        ServiceProvider = new ServiceCollection()
            .AddSingleton<ILoggerProvider>(arrayLoggingProvider)
            .AddSingleton<IArrayLoggingProvider>(arrayLoggingProvider)
            .AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .AddChainSharpEffects(
                options =>
                    options
                        .AddEffectWorkflowBus(
                            assemblies:
                            [
                                typeof(AssemblyMarker).Assembly,
                                typeof(ChainSharp.Tests.Effect.Integration.AssemblyMarker).Assembly
                            ]
                        )
                        .SetEffectLogLevel(LogLevel.Information)
                        .SaveWorkflowParameters()
                        .AddPostgresEffect(connectionString)
                        .AddEffectDataContextLogging(minimumLogLevel: LogLevel.Trace)
                        .AddJsonEffect()
                        .AddStepLogger(serializeStepData: true)
            )
            .BuildServiceProvider();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await ServiceProvider.DisposeAsync();
    }

    [SetUp]
    public virtual async Task TestSetUp()
    {
        Scope = ServiceProvider.CreateScope();
        WorkflowBus = Scope.ServiceProvider.GetRequiredService<IWorkflowBus>();

        var factory = Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();
        using var cleanupContext = (IDataContext)factory.Create();
        await CleanupDatabase(cleanupContext);
    }

    /// <summary>
    /// Deletes all rows from all scheduler tables in FK-safe order to ensure
    /// complete test isolation between runs.
    /// </summary>
    private static async Task CleanupDatabase(IDataContext dataContext)
    {
        // Delete in FK-safe order (children before parents)
        await dataContext.BackgroundJobs.ExecuteDeleteAsync();
        await dataContext.Logs.ExecuteDeleteAsync();
        await dataContext.WorkQueues.ExecuteDeleteAsync();
        await dataContext.DeadLetters.ExecuteDeleteAsync();
        await dataContext.Metadatas.ExecuteDeleteAsync();

        // Clear self-referencing FK before deleting manifests
        await dataContext
            .Manifests.Where(m => m.DependsOnManifestId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DependsOnManifestId, (int?)null));
        await dataContext.Manifests.ExecuteDeleteAsync();

        // Delete manifest groups after manifests (FK dependency)
        await dataContext.ManifestGroups.ExecuteDeleteAsync();

        dataContext.Reset();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        Scope.Dispose();
    }
}
