using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Extensions;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.Scheduler.Extensions;
using ChainSharp.Effect.Scheduler.Hangfire.Extensions;
using ChainSharp.Effect.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.HelloWorld;
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
builder.Services.AddChainSharpDashboard();

builder.Services.AddChainSharpEffects(
    options =>
        options
            // Register workflows from this assembly and the Scheduler assembly
            .AddEffectWorkflowBus(
                assemblies:
                [
                    typeof(Program).Assembly, // Sample workflows
                    typeof(ManifestExecutorWorkflow).Assembly, // Scheduler workflows
                ]
            )
            // Add Postgres for workflow metadata persistence
            .AddPostgresEffect(connectionString)
            .AddJsonEffect()
            .SaveWorkflowParameters()
            // Add Scheduler with Hangfire as the background task server
            .AddScheduler(
                scheduler =>
                    scheduler
                        .AddMetadataCleanup(
                            cleanup => cleanup.AddWorkflowType<IHelloWorldWorkflow>()
                        )
                        .UseHangfire(connectionString)
                        .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
                            "sample-hello-world",
                            new HelloWorldInput { Name = "ChainSharp Scheduler" },
                            Every.Seconds(20)
                        )
            )
);

var app = builder.Build();

app.UseChainSharpDashboard();
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });

app.Run();
