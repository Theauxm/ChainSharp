using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Hangfire.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.GoodbyeWorld;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.HelloWorld;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.TransformLoad;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString =
    builder.Configuration.GetConnectionString("ChainSharpDatabase")
    ?? throw new InvalidOperationException("Connection string 'ChainSharpDatabase' not found.");

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Add ChainSharp Effects with Postgres persistence, Scheduler, and Hangfire in one fluent call
builder.AddChainSharpDashboard();

builder.Services.AddChainSharpEffects(options =>
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
                })
                .UseHangfire(connectionString)
                .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
                    "sample-hello-world",
                    new HelloWorldInput { Name = "ChainSharp Scheduler" },
                    Every.Seconds(20)
                )
                // Dependent: GoodbyeWorld runs after HelloWorld succeeds
                .Then<IGoodbyeWorldWorkflow, GoodbyeWorldInput>(
                    "sample-goodbye-world",
                    new GoodbyeWorldInput { Name = "ChainSharp Scheduler" }
                )
                // Schedule ExtractImport for Customer table (10 indexes)
                .ScheduleMany<
                    IExtractImportWorkflow,
                    ExtractImportInput,
                    (string Table, int Index)
                >(
                    Enumerable.Range(0, 10).Select(i => ("Customer", i)),
                    source =>
                    (
                        $"extract-import-customer-{source.Index}",
                        new ExtractImportInput
                        {
                            TableName = source.Table,
                            Index = source.Index,
                        }
                    ),
                    Every.Minutes(5),
                    prunePrefix: "extract-import-customer-",
                    groupId: "extract-import-customer"
                )
                // Dependent: TransformLoad runs after each Customer ExtractImport succeeds
                .ThenMany<
                    ITransformLoadWorkflow,
                    TransformLoadInput,
                    (string Table, int Index)
                >(
                    Enumerable.Range(0, 10).Select(i => ("Customer", i)),
                    source =>
                    (
                        $"transform-load-customer-{source.Index}",
                        new TransformLoadInput
                        {
                            TableName = source.Table,
                            Index = source.Index,
                        }
                    ),
                    dependsOn: source => $"extract-import-customer-{source.Index}",
                    prunePrefix: "transform-load-customer-",
                    groupId: "transform-load-customer"
                )
                // Schedule ExtractImport for Transaction table (30 indexes)
                .ScheduleMany<
                    IExtractImportWorkflow,
                    ExtractImportInput,
                    (string Table, int Index)
                >(
                    Enumerable.Range(0, 30).Select(i => ("Transaction", i)),
                    source =>
                    (
                        $"extract-import-transaction-{source.Index}",
                        new ExtractImportInput
                        {
                            TableName = source.Table,
                            Index = source.Index,
                        }
                    ),
                    Every.Minutes(5),
                    prunePrefix: "extract-import-transaction-",
                    groupId: "extract-import-transaction"
                );
        })
);

var app = builder.Build();

app.UseChainSharpDashboard();
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });

app.Run();
