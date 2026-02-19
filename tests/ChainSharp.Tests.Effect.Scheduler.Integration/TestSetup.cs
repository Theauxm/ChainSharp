using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.ManifestGroup;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.StepProvider.Logging.Extensions;
using ChainSharp.Tests.ArrayLogger.Services.ArrayLoggingProvider;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.Effect.Scheduler.Integration;

[TestFixture]
public abstract class TestSetup
{
    private ServiceProvider ServiceProvider { get; set; } = null!;

    public IServiceScope Scope { get; private set; } = null!;

    public IWorkflowBus WorkflowBus { get; private set; } = null!;

    public ITaskServerExecutorWorkflow TaskServerExecutor { get; private set; } = null!;

    public IDataContext DataContext { get; private set; } = null!;

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
                                typeof(TaskServerExecutorWorkflow).Assembly,
                            ]
                        )
                        .SetEffectLogLevel(LogLevel.Information)
                        .SaveWorkflowParameters()
                        .AddPostgresEffect(connectionString)
                        .AddEffectDataContextLogging(minimumLogLevel: LogLevel.Trace)
                        .AddJsonEffect()
                        .AddStepLogger(serializeStepData: true)
                        .AddScheduler(
                            scheduler => scheduler.UseInMemoryTaskServer().AddMetadataCleanup()
                        )
            )
            // Register IDataContext as scoped, created from the factory
            .AddScoped<IDataContext>(sp =>
            {
                var factory = sp.GetRequiredService<IDataContextProviderFactory>();
                return (IDataContext)factory.Create();
            })
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
        TaskServerExecutor =
            Scope.ServiceProvider.GetRequiredService<ITaskServerExecutorWorkflow>();
        DataContext = Scope.ServiceProvider.GetRequiredService<IDataContext>();

        await CleanupDatabase(DataContext);
    }

    /// <summary>
    /// Deletes all rows from all scheduler tables in FK-safe order to ensure
    /// complete test isolation between runs.
    /// </summary>
    public static async Task CleanupDatabase(IDataContext dataContext)
    {
        // Delete in FK-safe order (children before parents)
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

    /// <summary>
    /// Creates and persists a ManifestGroup for test use.
    /// </summary>
    public static async Task<ManifestGroup> CreateAndSaveManifestGroup(
        IDataContext dataContext,
        string name = "test-group",
        int? maxActiveJobs = null,
        int priority = 0,
        bool isEnabled = true
    )
    {
        var group = new ManifestGroup
        {
            Name = name,
            MaxActiveJobs = maxActiveJobs,
            Priority = priority,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await dataContext.Track(group);
        await dataContext.SaveChanges(CancellationToken.None);
        dataContext.Reset();
        return group;
    }

    [TearDown]
    public async Task TestTearDown()
    {
        if (TaskServerExecutor is IDisposable taskServerDisposable)
            taskServerDisposable.Dispose();

        if (DataContext is IDisposable disposable)
            disposable.Dispose();

        Scope.Dispose();
    }
}
