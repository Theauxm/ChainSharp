using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.StepProvider.Progress.Extensions;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlwaysFails;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.DataQualityCheck;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.GoodbyeWorld;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.HelloWorld;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.TransformLoad;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString =
    builder.Configuration.GetConnectionString("ChainSharpDatabase")
    ?? throw new InvalidOperationException("Connection string 'ChainSharpDatabase' not found.");

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

// Add ChainSharp Effects with Postgres persistence, Scheduler, and Hangfire in one fluent call
builder.AddChainSharpDashboard();

builder.Services.AddChainSharpEffects(
    options =>
        options
            // Register workflows from this assembly and the Scheduler assembly
            .AddEffectWorkflowBus(
                assemblies:
                [
                    typeof(Program).Assembly, // Sample workflows
                    typeof(ManifestManagerWorkflow).Assembly, // Scheduler workflows
                ]
            )
            // Add Postgres for workflow metadata persistence
            .AddPostgresEffect(connectionString)
            .AddJsonEffect()
            .SaveWorkflowParameters()
            .AddStepProgress()
            // Add Scheduler with Hangfire as the background task server
            .AddScheduler(scheduler =>
            {
                scheduler
                    .AddMetadataCleanup(cleanup =>
                    {
                        cleanup.AddWorkflowType<IHelloWorldWorkflow>();
                        cleanup.AddWorkflowType<IGoodbyeWorldWorkflow>();
                        cleanup.AddWorkflowType<IExtractImportWorkflow>();
                        cleanup.AddWorkflowType<ITransformLoadWorkflow>();
                        cleanup.AddWorkflowType<IDataQualityCheckWorkflow>();
                        cleanup.AddWorkflowType<IAlwaysFailsWorkflow>();
                    })
                    .JobDispatcherPollingInterval(TimeSpan.FromSeconds(2))
                    .UsePostgresTaskServer();

                scheduler
                    .Schedule<IHelloWorldWorkflow>(
                        "sample-hello-world",
                        new HelloWorldInput { Name = "ChainSharp Scheduler" },
                        Every.Seconds(20)
                    )
                    .IncludeMany<IGoodbyeWorldWorkflow>(
                        Enumerable
                            .Range(0, 10)
                            .Select(
                                i =>
                                    new ManifestItem(
                                        $"sample-goodbye-number",
                                        new GoodbyeWorldInput() { Name = $"ChainSharp {i}" }
                                    )
                            )
                    )
                    // Fan-out: both GoodbyeWorld variants run after HelloWorld succeeds
                    // Include always branches from the root Schedule (HelloWorld)
                    .Include<IGoodbyeWorldWorkflow>(
                        "sample-goodbye-world",
                        new GoodbyeWorldInput { Name = "ChainSharp Scheduler" }
                    )
                    .Include<IGoodbyeWorldWorkflow>(
                        "sample-farewell-world",
                        new GoodbyeWorldInput { Name = "ChainSharp Farewell" }
                    )
                    .ThenInclude<IGoodbyeWorldWorkflow>(
                        "sample-otherword-world",
                        new GoodbyeWorldInput { Name = "ChainSharp otherword" }
                    );

                // AlwaysFails: intentionally throws on every run.
                // MaxRetries(1) means it dead-letters after a single failure,
                // providing a quick way to test the dead letter detail page.
                scheduler.Schedule<IAlwaysFailsWorkflow>(
                    "sample-always-fails",
                    new AlwaysFailsInput { Scenario = "Database connection timeout" },
                    Every.Seconds(30),
                    options => options.MaxRetries(1)
                );

                scheduler
                    // Schedule ExtractImport for Customer table (10 indexes)
                    .ScheduleMany<IExtractImportWorkflow>(
                        "extract-import-customer",
                        Enumerable
                            .Range(0, 10)
                            .Select(
                                i =>
                                    new ManifestItem(
                                        $"{i}",
                                        new ExtractImportInput { TableName = "Customer", Index = i }
                                    )
                            ),
                        Every.Minutes(5)
                    )
                    // First-level dependents: TransformLoad runs after each Customer ExtractImport succeeds
                    .IncludeMany<ITransformLoadWorkflow>(
                        "transform-load-customer",
                        Enumerable
                            .Range(0, 10)
                            .Select(
                                i =>
                                    new ManifestItem(
                                        $"{i}",
                                        new TransformLoadInput
                                        {
                                            TableName = "Customer",
                                            Index = i
                                        },
                                        DependsOn: $"extract-import-customer-{i}"
                                    )
                            )
                    )
                    // Dormant dependents: DataQualityCheck is declared in the topology but never
                    // auto-fires. The parent ExtractImport workflow activates it at runtime only
                    // when anomalies are detected, providing runtime-determined input.
                    .IncludeMany<IDataQualityCheckWorkflow>(
                        "dq-check-customer",
                        Enumerable
                            .Range(0, 10)
                            .Select(
                                i =>
                                    new ManifestItem(
                                        $"{i}",
                                        new DataQualityCheckInput
                                        {
                                            TableName = "Customer",
                                            Index = i,
                                            AnomalyCount = 0,
                                        },
                                        DependsOn: $"extract-import-customer-{i}"
                                    )
                            ),
                        options: o => o.Dormant()
                    );

                scheduler
                    // Schedule ExtractImport for Transaction table (30 indexes)
                    // Demonstrates per-group MaxActiveJobs via the new ScheduleOptions API
                    .ScheduleMany<IExtractImportWorkflow>(
                        "extract-import-transaction",
                        Enumerable
                            .Range(0, 30)
                            .Select(
                                i =>
                                    new ManifestItem(
                                        $"{i}",
                                        new ExtractImportInput
                                        {
                                            TableName = "Transaction",
                                            Index = i
                                        }
                                    )
                            ),
                        Every.Minutes(5),
                        options => options.Priority(24).Group(group => group.MaxActiveJobs(10))
                    )
                    // Dormant dependents for Transaction quality checks
                    .IncludeMany<IDataQualityCheckWorkflow>(
                        "dq-check-transaction",
                        Enumerable
                            .Range(0, 30)
                            .Select(
                                i =>
                                    new ManifestItem(
                                        $"{i}",
                                        new DataQualityCheckInput
                                        {
                                            TableName = "Transaction",
                                            Index = i,
                                            AnomalyCount = 0,
                                        },
                                        DependsOn: $"extract-import-transaction-{i}"
                                    )
                            ),
                        options: o => o.Dormant()
                    );
            })
);

var app = builder.Build();

app.UseChainSharpDashboard();

app.Run();
