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

var pollingInterval = TimeSpan.FromSeconds(
    builder.Configuration.GetValue<int>("Scheduler:PollingIntervalSeconds", 30)
);
var maxJobsPerCycle = builder.Configuration.GetValue<int>("Scheduler:MaxJobsPerCycle", 50);
var defaultMaxRetries = builder.Configuration.GetValue<int>("Scheduler:DefaultMaxRetries", 3);

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
                        .PollingInterval(pollingInterval)
                        .MaxJobsPerCycle(maxJobsPerCycle)
                        .DefaultMaxRetries(defaultMaxRetries)
                        .UseHangfire(
                            hangfireConfig =>
                                hangfireConfig
                                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                                    .UseSimpleAssemblyNameTypeSerializer()
                                    .UseRecommendedSerializerSettings()
                                    .UsePostgreSqlStorage(
                                        o => o.UseNpgsqlConnection(connectionString)
                                    ),
                            serverOptions =>
                            {
                                serverOptions.Queues = ["default", "scheduler"];
                            }
                        )
                        // Schedule the HelloWorld workflow to run every minute
                        // This replaces the manual SeedSampleManifestAsync method with a type-safe API
                        .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
                            "sample-hello-world",
                            new HelloWorldInput { Name = "ChainSharp Scheduler" },
                            Every.Minutes(1)
                        )
            )
);

var app = builder.Build();

// Enable Hangfire Dashboard (for debugging/monitoring)
app.UseHangfireDashboard(
    "/hangfire",
    new DashboardOptions
    {
        // In production, you'd add authorization here
        Authorization = []
    }
);

// Start the ChainSharp scheduler (manifest polling and seeds pending manifests)
app.UseChainSharpScheduler();

Console.WriteLine("=============================================================");
Console.WriteLine("ChainSharp Scheduler Sample with Hangfire");
Console.WriteLine("=============================================================");
Console.WriteLine();
Console.WriteLine($"Hangfire Dashboard: http://localhost:5000/hangfire");
Console.WriteLine($"Polling Interval: {pollingInterval.TotalSeconds} seconds");
Console.WriteLine($"Max Jobs Per Cycle: {maxJobsPerCycle}");
Console.WriteLine($"Default Max Retries: {defaultMaxRetries}");
Console.WriteLine();
Console.WriteLine("The ManifestManager will poll for scheduled jobs and execute them.");
Console.WriteLine("Check the Hangfire Dashboard to see job processing.");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop the application.");
Console.WriteLine("=============================================================");

app.Run("http://localhost:5000");
