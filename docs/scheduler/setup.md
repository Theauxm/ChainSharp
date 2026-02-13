---
layout: default
title: Setup
parent: Scheduling
nav_order: 1
---

# Setup & Creating Scheduled Workflows

## Quick Setup with Hangfire

### Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect.Orchestration.Scheduler
dotnet add package Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire
```

### Configuration

Jobs can be scheduled directly in startup configuration. The scheduler creates or updates manifests when the app starts:

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database");

builder.Services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(
        typeof(Program).Assembly,
        typeof(TaskServerExecutorWorkflow).Assembly
    )
    .AddPostgresEffect(connectionString)
    .AddScheduler(scheduler => scheduler
        .PollingInterval(TimeSpan.FromSeconds(5))
        .MaxJobsPerCycle(100)
        .DefaultMaxRetries(3)
        .UseHangfire(connectionString)

        // Schedule jobs directly in configuration
        .Schedule<IHelloWorldWorkflow, HelloWorldInput>(
            "hello-world",
            new HelloWorldInput { Name = "ChainSharp Scheduler" },
            Every.Minutes(1))

        .Schedule<IDailyReportWorkflow, DailyReportInput>(
            "daily-report",
            new DailyReportInput { ReportType = "sales" },
            Cron.Daily(hour: 3),
            opts => opts.MaxRetries = 5)
    )
);

var app = builder.Build();

app.UseHangfireDashboard("/hangfire");

app.Run();
```

`AddScheduler` registers a `BackgroundService` that handles manifest seeding and polling automatically—no extra startup call needed. Hangfire is configured internally; you only need to provide the connection string. Hangfire's automatic retries are disabled since the scheduler manages retries through the manifest system.

## Creating Scheduled Workflows

### 1. Define the Input

Your workflow input must implement `IManifestProperties`. This marker interface signals the type is safe for serialization and storage:

```csharp
public record SyncCustomersInput : IManifestProperties
{
    public string Region { get; init; } = "us-east";
    public int BatchSize { get; init; } = 1000;
}
```

Types without `IManifestProperties` won't compile with the scheduling API—this catches mistakes before runtime.

### 2. Create the Workflow

Standard `EffectWorkflow` with an interface for DI resolution:

```csharp
public interface ISyncCustomersWorkflow : IEffectWorkflow<SyncCustomersInput, Unit> { }

public class SyncCustomersWorkflow : EffectWorkflow<SyncCustomersInput, Unit>, ISyncCustomersWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(SyncCustomersInput input)
        => Activate(input)
            .Chain<FetchCustomersStep>()
            .Chain<TransformDataStep>()
            .Chain<WriteToDestinationStep>()
            .Resolve();
}
```

### 3. Schedule It

**Option A: Startup Configuration (recommended for static jobs)**

```csharp
.AddScheduler(scheduler => scheduler
    .UseHangfire(connectionString)
    .Schedule<ISyncCustomersWorkflow, SyncCustomersInput>(
        "sync-customers-us-east",
        new SyncCustomersInput { Region = "us-east", BatchSize = 500 },
        Cron.Hourly(minute: 30),
        opts => opts.MaxRetries = 3)
)
```

**Option B: Runtime via IManifestScheduler (for dynamic jobs)**

```csharp
public class JobSetupService(IManifestScheduler scheduler)
{
    public async Task SetupJobs()
    {
        await scheduler.ScheduleAsync<ISyncCustomersWorkflow, SyncCustomersInput>(
            "sync-customers-us-east",
            new SyncCustomersInput { Region = "us-east", BatchSize = 500 },
            Every.Hours(6),
            opts => opts.MaxRetries = 3);
    }
}
```

Both approaches use upsert semantics—the ExternalId determines whether to create or update the manifest.

## Schedule Helpers

The `Schedule` type defines when a job runs. Two helper classes make common patterns readable.

### Interval-Based: Every

For simple recurring jobs:

```csharp
Every.Seconds(30)    // Every 30 seconds
Every.Minutes(5)     // Every 5 minutes
Every.Hours(1)       // Every hour
Every.Days(1)        // Every day
```

### Cron-Based: Cron

For traditional cron schedules:

```csharp
Cron.Minutely()                           // * * * * *
Cron.Hourly(minute: 30)                   // 30 * * * *
Cron.Daily(hour: 3, minute: 0)            // 0 3 * * *
Cron.Weekly(DayOfWeek.Sunday, hour: 2)    // 0 2 * * 0
Cron.Monthly(day: 1, hour: 0)             // 0 0 1 * *
Cron.Expression("0 */6 * * *")            // Custom expression
```
