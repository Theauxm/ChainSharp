---
layout: default
title: Scheduling Options
parent: Scheduling
nav_order: 2
---

# Scheduling Options

## Bulk Scheduling

### Startup Configuration: ScheduleMany

For static bulk jobs, use the builder-time `ScheduleMany` during DI configuration. No async startup code or service resolution needed:

```csharp
var tables = new[] { "users", "orders", "products", "inventory" };

services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            tables,
            tableName => (
                ExternalId: $"sync-{tableName}",
                Input: new SyncTableInput { TableName = tableName }
            ),
            Every.Minutes(5))
    )
);
```

The builder captures the manifests and seeds them when the `BackgroundService` starts—same upsert semantics as `Schedule`.

### Grouping Manifests

When you schedule a batch of related manifests, pass a `groupId` to tie them together:

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            tables,
            tableName => ($"sync-{tableName}", new SyncTableInput { TableName = tableName }),
            Every.Minutes(5),
            groupId: "table-sync")
    )
);
```

The `groupId` is stored on each `Manifest` in the batch. It's optional—manifests without a group work exactly as before. The dashboard's **Manifest Groups** page aggregates execution stats across all manifests sharing a group, which is useful when a logical operation is split across many manifests (e.g., syncing 1000 table slices).

### Runtime: ScheduleManyAsync

`ScheduleManyAsync` creates multiple manifests in a single transaction at runtime. If any fails, the entire batch rolls back.

```csharp
var tables = new[] { "users", "orders", "products", "inventory" };

await scheduler.ScheduleManyAsync<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    tableName => (
        ExternalId: $"sync-{tableName}",
        Input: new SyncTableInput { TableName = tableName }
    ),
    Every.Minutes(5),
    groupId: "table-sync");
```

Use the builder approach for jobs known at compile time. Use `ScheduleManyAsync` when the set of jobs is determined at runtime (loaded from a database, config file, or external API). Both variants accept the same `groupId` parameter.

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

### Pruning Stale Manifests

When the source collection shrinks between deployments—tables removed, slices reduced—old manifests stick around in the database. The `prunePrefix` parameter handles this automatically:

```csharp
// If "partner" was removed from this list since the last deployment,
// its manifests will be deleted because they start with "sync-"
// but weren't included in the current batch.
var tables = new[] { "customer", "user" };

scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => (
        ExternalId: $"sync-{table}",
        Input: new SyncTableInput { TableName = table }
    ),
    Every.Minutes(5),
    prunePrefix: "sync-");
```

When `prunePrefix` is specified, after creating or updating the manifests in the batch, the scheduler deletes any existing manifests whose `ExternalId` starts with the prefix but weren't part of the current call. This keeps the manifest table in sync with your source data without manual cleanup.

## Management Operations

`IManifestScheduler` includes methods for runtime job control:

```csharp
// Disable a job (e.g., during maintenance)
await scheduler.DisableAsync("sync-users");

// Re-enable
await scheduler.EnableAsync("sync-users");

// Trigger immediate execution (doesn't wait for schedule)
await scheduler.TriggerAsync("sync-users");

// Schedule a dependent job at runtime
await scheduler.ScheduleDependentAsync<ILoadWorkflow, LoadInput>(
    "load-users",
    new LoadInput { Table = "users" },
    dependsOnExternalId: "sync-users");
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
| `Dependent` | Runs after another manifest succeeds | `.Then()` / `.ThenMany()` or `ScheduleDependentAsync` |
| `None` | Manual trigger only | Use `scheduler.TriggerAsync(externalId)` |

See [Dependent Workflows](dependent-workflows.md) for details on chaining workflows.

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `PollingInterval` | 5s | How often ManifestManager checks for pending jobs (supports sub-minute) |
| `MaxActiveJobs` | 100 | Maximum active jobs (Pending + InProgress) allowed. Enforced by the [JobDispatcher](admin-workflows/job-dispatcher.md) at dispatch time. Set to null for unlimited |
| `DefaultMaxRetries` | 3 | Retry attempts before dead-lettering |
| `DefaultRetryDelay` | 5m | Delay between retries |
| `RetryBackoffMultiplier` | 2.0 | Exponential backoff (delays of 5m, 10m, 20m...) |
| `MaxRetryDelay` | 1h | Cap on backoff growth |
| `DefaultJobTimeout` | 1h | When a job is considered stuck |
| `RecoverStuckJobsOnStartup` | true | Re-evaluate stuck jobs on startup |
