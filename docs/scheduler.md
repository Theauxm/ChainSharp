---
layout: default
title: Scheduling
nav_order: 6
---

# Scheduling

ChainSharp.Effect.Scheduler adds background job orchestration to workflows. Define a manifest (what to run, when, and how many retries), and the scheduler handles execution, retries, and dead-lettering.

This isn't a traditional cron scheduler. It supports cron expressions, but its design goal is controlled bulk job orchestration—database replication with thousands of table slices, for example—where you need visibility into every execution attempt.

## When to Use the Scheduler

A hosted service with a timer works fine for simple recurring tasks. The Scheduler is for when you need the audit trail: every execution recorded with inputs, outputs, timing, and failure details. Failed jobs retry automatically. Jobs that fail too many times go to a dead letter queue for manual review.

## Core Concepts

### Manifest = Job Definition

A `Manifest` describes a type of job: which workflow it triggers, scheduling rules, retry policies, and default configuration. The `IManifestScheduler` handles the boilerplate—no need to worry about assembly-qualified names or JSON serialization:

```csharp
await scheduler.ScheduleAsync<ISyncCustomersWorkflow, SyncCustomersInput>(
    "sync-customers-us-east",
    new SyncCustomersInput { Region = "us-east", BatchSize = 500 },
    Every.Hours(6),
    opts => opts.MaxRetries = 3);
```

The scheduler creates the manifest, resolves the correct type names, and serializes the input automatically. Every call is an upsert—safe to run on every startup without duplicating jobs.

### Metadata = Execution Record

Each time a manifest runs, it creates a new `Metadata` record. These are **immutable**—retries create new rows, never mutate existing ones. This gives you a complete audit trail:

```
Manifest: "sync-customers-us-east"
├── Metadata #1: Completed at 10:00:00
├── Metadata #2: Failed at 10:05:00 (timeout)
├── Metadata #3: Failed at 10:10:00 (timeout)
├── Metadata #4: Failed at 10:15:00 (timeout) → Dead-lettered
```

### Dead Letter = Failed Beyond Retry

When a job fails more times than `MaxRetries`, it moves to the dead letter queue. Dead letters require manual intervention—the scheduler won't automatically retry them.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Dead Letter                               │
├─────────────────────────────────────────────────────────────────┤
│ Status: AwaitingIntervention                                    │
│ Reason: Max retries exceeded (3 failures >= 3 max retries)      │
│ DeadLetteredAt: 2026-02-10 10:15:00                            │
└─────────────────────────────────────────────────────────────────┘
```

Operators can retry (which creates a new execution) or acknowledge (mark as handled without retry).

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│              ManifestPollingService (BackgroundService)           │
│          Polls ManifestManager on a configurable interval        │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ManifestManagerWorkflow                         │
│                                                                  │
│  LoadManifests → ReapFailedJobs → DetermineJobsToQueue →        │
│                                        EnqueueJobs               │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ Enqueues jobs to Hangfire
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ManifestExecutorWorkflow                        │
│                  (runs on Hangfire workers)                       │
│                                                                  │
│  LoadMetadata → ValidateState → ExecuteWorkflow →               │
│                                      UpdateManifest              │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Your Workflow                                │
│              (Resolved via WorkflowBus)                          │
└─────────────────────────────────────────────────────────────────┘
```

The **ManifestPollingService** is a .NET `BackgroundService` that runs the ManifestManager on a configurable interval. It supports sub-minute polling (e.g., every 5 seconds)—something that wasn't possible when the manager was triggered by a Hangfire cron job. On startup, it also seeds any manifests configured via `.Schedule()` or `.ScheduleMany()`.

The **ManifestManagerWorkflow** loads enabled manifests, dead-letters any that have exceeded their retry limit, determines which are due for execution, and enqueues them to the background task server (Hangfire).

The **ManifestExecutorWorkflow** runs on Hangfire workers for each enqueued job. It loads the Metadata and Manifest, validates the job is still pending, executes the target workflow via `IWorkflowBus`, and updates `LastSuccessfulRun` on success.

## Quick Setup with Hangfire

### Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect.Scheduler
dotnet add package Theauxm.ChainSharp.Effect.Scheduler.Hangfire
```

### Configuration

Jobs can be scheduled directly in startup configuration. The scheduler creates or updates manifests when the app starts:

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database");

builder.Services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(
        typeof(Program).Assembly,
        typeof(ManifestExecutorWorkflow).Assembly
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

## Bulk Scheduling

`ScheduleManyAsync` creates multiple manifests in a single transaction. If any fails, the entire batch rolls back.

### Simple List

```csharp
var tables = new[] { "users", "orders", "products", "inventory" };

await scheduler.ScheduleManyAsync<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    tableName => (
        ExternalId: $"sync-{tableName}",
        Input: new SyncTableInput { TableName = tableName }
    ),
    Every.Minutes(5));
```

### Configuration Per Item

The optional `configure` parameter receives each source item, so you can vary settings:

```csharp
var tableConfigs = new[]
{
    (Name: "users", Interval: TimeSpan.FromMinutes(1), Retries: 5),
    (Name: "orders", Interval: TimeSpan.FromMinutes(1), Retries: 5),
    (Name: "products", Interval: TimeSpan.FromMinutes(15), Retries: 3),
    (Name: "logs", Interval: TimeSpan.FromHours(1), Retries: 1),
};

foreach (var config in tableConfigs)
{
    await scheduler.ScheduleAsync<ISyncTableWorkflow, SyncTableInput>(
        $"sync-{config.Name}",
        new SyncTableInput { TableName = config.Name },
        Schedule.FromInterval(config.Interval),
        opts => opts.MaxRetries = config.Retries);
}
```

### Multi-Dimensional Bulk Jobs

For jobs split across multiple dimensions (e.g., table × slice index):

```csharp
var tables = new[] 
{ 
    (Name: "customer", SliceCount: 100), 
    (Name: "partner", SliceCount: 10), 
    (Name: "user", SliceCount: 1000) 
};

// Flatten with LINQ, schedule all in one transaction
var allJobs = tables.SelectMany(t => 
    Enumerable.Range(0, t.SliceCount).Select(slice => (t.Name, slice)));

await scheduler.ScheduleManyAsync<ISyncTableWorkflow, SyncTableInput, (string Table, int Slice)>(
    allJobs,
    item => (
        ExternalId: $"sync-{item.Table}-{item.Slice}",
        Input: new SyncTableInput { TableName = item.Table, SliceIndex = item.Slice }
    ),
    Every.Minutes(5));
```

## Management Operations

`IManifestScheduler` includes methods for runtime job control:

```csharp
// Disable a job (e.g., during maintenance)
await scheduler.DisableAsync("sync-users");

// Re-enable
await scheduler.EnableAsync("sync-users");

// Trigger immediate execution (doesn't wait for schedule)
await scheduler.TriggerAsync("sync-users");
```

Disabled jobs remain in the database but are skipped by the ManifestManager until re-enabled.

## Manifest Options

Configure per-job settings via the `ManifestOptions` callback:

```csharp
await scheduler.ScheduleAsync<IMyWorkflow, MyInput>(
    "my-job",
    new MyInput { ... },
    Every.Hours(1),
    opts =>
    {
        opts.IsEnabled = true;      // Default: true
        opts.MaxRetries = 5;        // Default: 3
        opts.Timeout = TimeSpan.FromMinutes(30);  // Null uses global default
    });
```

## Schedule Types

| Type | Use Case | API |
|------|----------|-----|
| `Interval` | Simple recurring | `Every.Minutes(5)` or `Schedule.FromInterval(TimeSpan)` |
| `Cron` | Traditional scheduling | `Cron.Daily()` or `Schedule.FromCron("0 3 * * *")` |
| `None` | Manual trigger only | Use `scheduler.TriggerAsync(externalId)` |

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `PollingInterval` | 5s | How often ManifestManager checks for pending jobs (supports sub-minute) |
| `MaxJobsPerCycle` | 100 | Maximum jobs enqueued per poll (prevents overwhelming workers) |
| `DefaultMaxRetries` | 3 | Retry attempts before dead-lettering |
| `DefaultRetryDelay` | 5m | Delay between retries |
| `RetryBackoffMultiplier` | 2.0 | Exponential backoff (delays of 5m, 10m, 20m...) |
| `MaxRetryDelay` | 1h | Cap on backoff growth |
| `DefaultJobTimeout` | 1h | When a job is considered stuck |
| `RecoverStuckJobsOnStartup` | true | Re-evaluate stuck jobs on startup |

## Handling Dead Letters

When a job exceeds `MaxRetries`, it enters the dead letter queue with status `AwaitingIntervention`. The ManifestManager will skip these manifests until they're resolved.

To resolve a dead letter:

```csharp
// Option 1: Retry (creates a new execution)
deadLetter.Status = DeadLetterStatus.Retried;
deadLetter.ResolvedAt = DateTime.UtcNow;
deadLetter.ResolutionNote = "Root cause fixed, retrying";
// Then create a new Metadata and enqueue it

// Option 2: Acknowledge (mark as handled, no retry)
deadLetter.Status = DeadLetterStatus.Acknowledged;
deadLetter.ResolvedAt = DateTime.UtcNow;
deadLetter.ResolutionNote = "Data was manually corrected";

await context.SaveChanges(ct);
```

## Monitoring

The Hangfire Dashboard at `/hangfire` shows enqueued ManifestExecutor jobs, failures, and worker health. The ManifestManager polling itself runs as a .NET `BackgroundService` outside of Hangfire, so it won't appear in the dashboard. Configure authorization for production.

For workflow-level details, query the `Metadata` table:

```csharp
// Recent failures for a manifest
var failures = await context.Metadatas
    .Where(m => m.ManifestId == manifestId && m.WorkflowState == WorkflowState.Failed)
    .OrderByDescending(m => m.StartTime)
    .Take(10)
    .ToListAsync();
```

## Metadata Cleanup

System workflows like `ManifestManagerWorkflow` run frequently (every 5 seconds by default), generating metadata rows that have no long-term value. The metadata cleanup service automatically purges old entries to keep the database clean.

### How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│        MetadataCleanupPollingService (BackgroundService)         │
│            Polls on CleanupInterval (default: 1 minute)         │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MetadataCleanupWorkflow                         │
│                                                                  │
│  DeleteExpiredMetadataStep:                                      │
│    1. Find metadata matching whitelist + older than retention    │
│    2. Only terminal states (Completed / Failed)                  │
│    3. Delete associated log entries (FK safety)                  │
│    4. Delete metadata rows                                       │
└─────────────────────────────────────────────────────────────────┘
```

The cleanup only targets metadata in **terminal states** (Completed or Failed). Pending and InProgress metadata is never deleted, regardless of age. Associated log entries are deleted first to avoid foreign key constraint violations.

Deletion uses EF Core's `ExecuteDeleteAsync` for efficient single-statement SQL—no entities are loaded into memory.

### Enabling Cleanup

Add `.AddMetadataCleanup()` to your scheduler configuration:

```csharp
builder.Services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .AddMetadataCleanup()
    )
);
```

With no configuration, this cleans up `ManifestManagerWorkflow` and `MetadataCleanupWorkflow` metadata older than 1 hour, checking every minute.

### Custom Configuration

Override defaults and add your own workflow types to the whitelist:

```csharp
.AddScheduler(scheduler => scheduler
    .UseHangfire(connectionString)
    .AddMetadataCleanup(cleanup =>
    {
        cleanup.RetentionPeriod = TimeSpan.FromHours(2);
        cleanup.CleanupInterval = TimeSpan.FromMinutes(5);
        cleanup.AddWorkflowType<IMyNoisyWorkflow>();
        cleanup.AddWorkflowType("LegacyWorkflowName");
    })
)
```

`AddWorkflowType<T>()` uses `typeof(T).Name` to match the `Name` column in the metadata table—the same value workflows record when they execute. You can also pass a raw string for workflows that aren't easily referenced by type.

### Cleanup Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `CleanupInterval` | 1 minute | How often the cleanup service runs |
| `RetentionPeriod` | 1 hour | How long to keep metadata before it becomes eligible for deletion |
| `WorkflowTypeWhitelist` | `ManifestManagerWorkflow`, `MetadataCleanupWorkflow` | Workflow names whose metadata can be deleted (append via `AddWorkflowType`) |

### What Gets Deleted

A metadata row is deleted when **all** of these conditions are true:

1. Its `Name` matches a workflow in the whitelist
2. Its `StartTime` is older than the retention period
3. Its `WorkflowState` is `Completed` or `Failed`

Any log entries associated with deleted metadata are also removed.

## Testing

For integration tests, use the in-memory task server instead of Hangfire:

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler.UseInMemoryTaskServer())
);
```

Jobs execute inline, so tests are fast and don't need Hangfire infrastructure.
