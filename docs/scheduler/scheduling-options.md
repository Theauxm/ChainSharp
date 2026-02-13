---
layout: default
title: Scheduling Options
parent: Scheduling
nav_order: 2
---

# Scheduling Options

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

For jobs split across multiple dimensions (e.g., table x slice index):

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
