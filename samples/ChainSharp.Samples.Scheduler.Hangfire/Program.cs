using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Extensions;
using ChainSharp.Effect.Scheduler.Extensions;
using ChainSharp.Effect.Scheduler.Hangfire.Extensions;
using ChainSharp.Effect.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.HelloWorld;
using Hangfire;
using Hangfire.PostgreSql;

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
            // Add Scheduler with Hangfire as the background task server
            .AddScheduler(
                scheduler =>
                    scheduler
                        .UseHangfire(
                            h =>
                                h.UsePostgreSqlStorage(
                                    o => o.UseNpgsqlConnection(connectionString)
                                ),
                            serverOptions =>
                            {
                                serverOptions.Queues = ["default", "scheduler"];
                            }
                        )
                        .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
                            "sample-hello-world",
                            new HelloWorldInput { Name = "ChainSharp Scheduler" },
                            Every.Minutes(1)
                        )
            )
);

var app = builder.Build();

app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });

// Start the ChainSharp scheduler (manifest polling and seeds pending manifests)
app.UseChainSharpScheduler();
app.Run();
