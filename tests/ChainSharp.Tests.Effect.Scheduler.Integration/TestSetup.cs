using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Extensions;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.Scheduler.Extensions;
using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;
using ChainSharp.Effect.StepProvider.Logging.Extensions;
using ChainSharp.Tests.ArrayLogger.Services.ArrayLoggingProvider;
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

    public IManifestExecutorWorkflow ManifestExecutor { get; private set; } = null!;

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
                                typeof(ManifestExecutorWorkflow).Assembly,
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
        ManifestExecutor = Scope.ServiceProvider.GetRequiredService<IManifestExecutorWorkflow>();
        DataContext = Scope.ServiceProvider.GetRequiredService<IDataContext>();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        if (ManifestExecutor is IDisposable manifestDisposable)
            manifestDisposable.Dispose();

        if (DataContext is IDisposable disposable)
            disposable.Dispose();

        Scope.Dispose();
    }
}
