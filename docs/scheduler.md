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

A `Manifest` describes a type of job: which workflow it triggers, scheduling rules, retry policies, and default configuration.

```csharp
var manifest = new Manifest
{
    Name = typeof(IReplicateTableWorkflow).AssemblyQualifiedName!,
    PropertyTypeName = typeof(ReplicationInput).AssemblyQualifiedName,
    Properties = JsonSerializer.Serialize(new ReplicationInput { TableName = "users" }),
    IsEnabled = true,
    ScheduleType = ScheduleType.Interval,
    IntervalSeconds = 300,  // Every 5 minutes
    MaxRetries = 3
};
```

### Metadata = Execution Record

Each time a manifest runs, it creates a new `Metadata` record. These are **immutable**—retries create new rows, never mutate existing ones. This gives you a complete audit trail:

```
Manifest: "ReplicateUsers"
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
│                Background Task Server (Hangfire)                 │
│              Triggers ManifestManager on schedule                │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ManifestManagerWorkflow                         │
│                                                                  │
│  LoadManifests → ReapFailedJobs → DetermineJobsToQueue →        │
│                                        EnqueueJobs               │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ Enqueues jobs
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ManifestExecutorWorkflow                        │
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

The **ManifestManagerWorkflow** runs on a recurring schedule (every minute by default). It loads enabled manifests, dead-letters any that have exceeded their retry limit, determines which are due for execution, and enqueues them to the background task server.

The **ManifestExecutorWorkflow** runs for each enqueued job. It loads the Metadata and Manifest, validates the job is still pending, executes the target workflow via `IWorkflowBus`, and updates `LastSuccessfulRun` on success.

## Quick Setup with Hangfire

### Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect.Scheduler
dotnet add package Theauxm.ChainSharp.Effect.Scheduler.Hangfire
```

### Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database");

// All ChainSharp configuration in one fluent call
builder.Services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(
        typeof(Program).Assembly,                    // Your workflows
        typeof(ManifestExecutorWorkflow).Assembly    // Scheduler workflows
    )
    .AddPostgresEffect(connectionString)
    .AddScheduler(scheduler => scheduler
        .PollingInterval(TimeSpan.FromSeconds(30))
        .MaxJobsPerCycle(100)
        .DefaultMaxRetries(3)
        .UseHangfire(
            config => config.UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(connectionString)),
            server => server.WorkerCount = Environment.ProcessorCount * 2
        )
    )
);

var app = builder.Build();

// Enable Hangfire Dashboard and start the scheduler
app.UseHangfireDashboard("/hangfire");
app.UseChainSharpScheduler();

app.Run();
```

## Creating Scheduled Workflows

### 1. Define the Input

Your workflow input must implement `IManifestProperties` so the scheduler can serialize it:

```csharp
public record SyncCustomersInput : IManifestProperties
{
    public string Region { get; init; } = "us-east";
    public int BatchSize { get; init; } = 1000;
}
```

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

### 3. Create a Manifest

Manifests are typically created via an API, admin interface, or seeding during startup:

```csharp
var manifest = new Manifest
{
    ExternalId = "sync-customers-us-east",
    Name = typeof(ISyncCustomersWorkflow).AssemblyQualifiedName!,
    PropertyTypeName = typeof(SyncCustomersInput).AssemblyQualifiedName,
    Properties = JsonSerializer.Serialize(new SyncCustomersInput
    {
        Region = "us-east",
        BatchSize = 500
    }),
    IsEnabled = true,
    ScheduleType = ScheduleType.Cron,
    CronExpression = "0 */6 * * *",  // Every 6 hours
    MaxRetries = 3,
    TimeoutSeconds = 3600  // 1 hour timeout
};

context.Manifests.Add(manifest);
await context.SaveChanges(ct);
```

The workflow interface's assembly-qualified name goes in `Name`. The scheduler resolves it via `IWorkflowBus` the same way any other workflow runs.

## Schedule Types

| Type | Use Case | Configuration |
|------|----------|---------------|
| `None` | Manual trigger only | Call `ManifestManager.TriggerManifestAsync()` |
| `Cron` | Traditional scheduling | Set `CronExpression` (e.g., `"0 3 * * *"` for daily at 3am) |
| `Interval` | Simple recurring | Set `IntervalSeconds` (e.g., `300` for every 5 minutes) |
| `OnDemand` | Bulk operations | Designed for programmatic bulk enqueuing |

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `PollingInterval` | 60s | How often ManifestManager checks for pending jobs |
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

The Hangfire Dashboard at `/hangfire` shows queued jobs, failures, recurring schedules, and worker health. Configure authorization for production.

For workflow-level details, query the `Metadata` table:

```csharp
// Recent failures for a manifest
var failures = await context.Metadatas
    .Where(m => m.ManifestId == manifestId && m.WorkflowState == WorkflowState.Failed)
    .OrderByDescending(m => m.StartTime)
    .Take(10)
    .ToListAsync();
```

## Testing

For integration tests, use the in-memory task server instead of Hangfire:

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler.UseInMemoryTaskServer())
);
```

Jobs execute inline, so tests are fast and don't need Hangfire infrastructure.
