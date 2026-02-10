using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Extensions;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Scheduler.Extensions;
using ChainSharp.Effect.Scheduler.Hangfire.Extensions;
using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.HelloWorld;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("ChainSharpDatabase")
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
builder.Services.AddChainSharpEffects(options => options
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
    // Add JSON logging for debugging
    .AddJsonEffect()
    // Add Scheduler with Hangfire as the background task server
    .AddScheduler(scheduler => scheduler
        .PollingInterval(pollingInterval)
        .MaxJobsPerCycle(maxJobsPerCycle)
        .DefaultMaxRetries(defaultMaxRetries)
        .UseHangfire(
            hangfireConfig => hangfireConfig
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)),
            serverOptions => { serverOptions.Queues = ["default", "scheduler"]; }
        )
    )
);

var app = builder.Build();

// Enable Hangfire Dashboard (for debugging/monitoring)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // In production, you'd add authorization here
    Authorization = []
});

// Start the ChainSharp scheduler (manifest polling)
app.UseChainSharpScheduler();

// Seed a sample manifest for demonstration
await SeedSampleManifestAsync(app.Services, connectionString);

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

/// <summary>
/// Seeds a sample manifest for demonstration purposes.
/// In a real application, manifests would be created via an API or admin interface.
/// </summary>
static async Task SeedSampleManifestAsync(IServiceProvider services, string connectionString)
{
    // Use a separate scope for seeding
    using var scope = services.CreateScope();
    var dataContextFactory = scope.ServiceProvider.GetService<IDataContextProviderFactory>();

    if (dataContextFactory == null)
    {
        Console.WriteLine("Warning: DataContextFactory not available. Skipping manifest seeding.");
        return;
    }

    try
    {
        using var context = dataContextFactory.Create() as IDataContext;
        if (context == null) return;

        // Check if we already have a sample manifest
        var existingManifest = context.Manifests
            .FirstOrDefault(m => m.ExternalId == "sample-hello-world");

        if (existingManifest == null)
        {
            // Create a sample manifest that runs every minute
            var manifest = new Manifest
            {
                ExternalId = "sample-hello-world",
                Name = typeof(IHelloWorldWorkflow).AssemblyQualifiedName!,
                PropertyTypeName = typeof(HelloWorldInput).AssemblyQualifiedName,
                Properties = System.Text.Json.JsonSerializer.Serialize(new HelloWorldInput
                {
                    Name = "ChainSharp Scheduler"
                }),
                IsEnabled = true,
                ScheduleType = ScheduleType.Interval,
                IntervalSeconds = 60, // Run every 60 seconds
                MaxRetries = 3
            };

            context.Manifests.Add(manifest);
            await context.SaveChanges(CancellationToken.None);

            Console.WriteLine("Seeded sample 'HelloWorld' manifest (runs every 60 seconds)");
        }
        else
        {
            Console.WriteLine("Sample 'HelloWorld' manifest already exists");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not seed sample manifest: {ex.Message}");
    }
}
