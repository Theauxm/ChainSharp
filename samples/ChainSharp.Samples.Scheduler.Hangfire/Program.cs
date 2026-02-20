using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
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
                    })
                    .JobDispatcherPollingInterval(TimeSpan.FromSeconds(2))
                    .UsePostgresTaskServer();

                scheduler
                    .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
                        "sample-hello-world",
                        new HelloWorldInput { Name = "ChainSharp Scheduler" },
                        Every.Seconds(20)
                    )
                    .IncludeMany<IGoodbyeWorldWorkflow, GoodbyeWorldInput, string>(
                        Enumerable.Range(0, 10).Select(i => ($"ChainSharp {i}")),
                        source =>
                            ("sample-goodbye-number", new GoodbyeWorldInput() { Name = source })
                    )
                    // Fan-out: both GoodbyeWorld variants run after HelloWorld succeeds
                    // Include always branches from the root Schedule (HelloWorld)
                    .Include<IGoodbyeWorldWorkflow, GoodbyeWorldInput>(
                        "sample-goodbye-world",
                        new GoodbyeWorldInput { Name = "ChainSharp Scheduler" }
                    )
                    .Include<IGoodbyeWorldWorkflow, GoodbyeWorldInput>(
                        "sample-farewell-world",
                        new GoodbyeWorldInput { Name = "ChainSharp Farewell" }
                    )
                    .ThenInclude<IGoodbyeWorldWorkflow, GoodbyeWorldInput>(
                        "sample-otherword-world",
                        new GoodbyeWorldInput { Name = "ChainSharp otherword" }
                    );

                scheduler
                    // Schedule ExtractImport for Customer table (10 indexes)
                    .ScheduleMany<
                        IExtractImportWorkflow,
                        ExtractImportInput,
                        (string Table, int Index)
                    >(
                        "extract-import-customer",
                        Enumerable.Range(0, 10).Select(i => ("Customer", i)),
                        source =>
                            (
                                $"{source.Index}",
                                new ExtractImportInput
                                {
                                    TableName = source.Table,
                                    Index = source.Index,
                                }
                            ),
                        Every.Minutes(5)
                    )
                    // First-level dependents: TransformLoad runs after each Customer ExtractImport succeeds
                    .IncludeMany<
                        ITransformLoadWorkflow,
                        TransformLoadInput,
                        (string Table, int Index)
                    >(
                        "transform-load-customer",
                        Enumerable.Range(0, 10).Select(i => ("Customer", i)),
                        source =>
                            (
                                $"{source.Index}",
                                new TransformLoadInput
                                {
                                    TableName = source.Table,
                                    Index = source.Index,
                                }
                            ),
                        dependsOn: source => $"extract-import-customer-{source.Index}"
                    )
                    // Dormant dependents: DataQualityCheck is declared in the topology but never
                    // auto-fires. The parent ExtractImport workflow activates it at runtime only
                    // when anomalies are detected, providing runtime-determined input.
                    .IncludeMany<
                        IDataQualityCheckWorkflow,
                        DataQualityCheckInput,
                        (string Table, int Index)
                    >(
                        "dq-check-customer",
                        Enumerable.Range(0, 10).Select(i => ("Customer", i)),
                        source =>
                            (
                                $"{source.Index}",
                                new DataQualityCheckInput
                                {
                                    TableName = source.Table,
                                    Index = source.Index,
                                    AnomalyCount = 0,
                                }
                            ),
                        dependsOn: source => $"extract-import-customer-{source.Index}",
                        options: o => o.Dormant()
                    );

                scheduler
                    // Schedule ExtractImport for Transaction table (30 indexes)
                    // Demonstrates per-group MaxActiveJobs via the new ScheduleOptions API
                    .ScheduleMany<
                        IExtractImportWorkflow,
                        ExtractImportInput,
                        (string Table, int Index)
                    >(
                        "extract-import-transaction",
                        Enumerable.Range(0, 30).Select(i => ("Transaction", i)),
                        source =>
                            (
                                $"{source.Index}",
                                new ExtractImportInput
                                {
                                    TableName = source.Table,
                                    Index = source.Index,
                                }
                            ),
                        Every.Minutes(5),
                        options => options.Priority(24).Group(group => group.MaxActiveJobs(10))
                    )
                    // Dormant dependents for Transaction quality checks
                    .IncludeMany<
                        IDataQualityCheckWorkflow,
                        DataQualityCheckInput,
                        (string Table, int Index)
                    >(
                        "dq-check-transaction",
                        Enumerable.Range(0, 30).Select(i => ("Transaction", i)),
                        source =>
                            (
                                $"{source.Index}",
                                new DataQualityCheckInput
                                {
                                    TableName = source.Table,
                                    Index = source.Index,
                                    AnomalyCount = 0,
                                }
                            ),
                        dependsOn: source => $"extract-import-transaction-{source.Index}",
                        options: o => o.Dormant()
                    );
            })
);

var app = builder.Build();

app.UseChainSharpDashboard();

app.Run();
