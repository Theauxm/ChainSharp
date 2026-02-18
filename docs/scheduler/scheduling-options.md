---
layout: default
title: Scheduling Options
parent: Scheduling
nav_order: 2
---

# Scheduling Options

## Bulk Scheduling

### Startup Configuration: ScheduleMany

For static bulk jobs, use the builder-time `ScheduleMany` during DI configuration. No async startup code or service resolution needed. The name-based overload is the simplest approach — it derives `groupId`, `prunePrefix`, and the external ID prefix from a single `name` parameter:

```csharp
var tables = new[] { "users", "orders", "products", "inventory" };

services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            "sync",
            tables,
            tableName => (tableName, new SyncTableInput { TableName = tableName }),
            Every.Minutes(5))
    )
);
// Creates: sync-users, sync-orders, sync-products, sync-inventory
// groupId: "sync", prunePrefix: "sync-"
```

For full control over `groupId`, `prunePrefix`, and external IDs independently, use the explicit overload:

```csharp
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    tableName => ($"sync-{tableName}", new SyncTableInput { TableName = tableName }),
    Every.Minutes(5),
    prunePrefix: "sync-",
    groupId: "sync");
```

*API Reference: [ScheduleMany]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %})*

The builder captures the manifests and seeds them when the `BackgroundService` starts—same upsert semantics as `Schedule`.

### Grouping Manifests

Every manifest belongs to a **ManifestGroup**. A ManifestGroup is a first-class entity that ties related manifests together and exposes per-group dispatch controls (see [Per-Group Dispatch Controls](#per-group-dispatch-controls) below).

The `groupId` parameter is available on `Schedule`, `ThenInclude`, `Include`, `ScheduleMany`, `ThenIncludeMany`, and `IncludeMany`. When you don't specify a `groupId`, it defaults to the manifest's `externalId`—so every manifest always has a group, even if it's a group of one. ManifestGroups are upserted by name during scheduling: if a group with that name already exists it's reused, otherwise a new one is created automatically. Orphaned groups (groups with no remaining manifests) are cleaned up on startup.

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        // Single manifest — explicit groupId shared with other related jobs
        .Schedule<IExtractWorkflow, ExtractInput>(
            "extract-users",
            new ExtractInput { Table = "users" },
            Every.Minutes(5),
            groupId: "user-pipeline")
        // Dependent manifest in the same group
        .Include<ILoadWorkflow, LoadInput>(
            "load-users",
            new LoadInput { Table = "users" },
            groupId: "user-pipeline")
        // Bulk scheduling — name-based overload sets groupId automatically
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            "table-sync",
            tables,
            tableName => (tableName, new SyncTableInput { TableName = tableName }),
            Every.Minutes(5))
    )
);
```

*API Reference: [Schedule]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule.md %}), [ScheduleMany]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %})*

The dashboard's **Manifest Groups** page shows each group's settings and aggregate execution stats, which is useful when a logical operation is split across many manifests (e.g., syncing 1000 table slices).

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

*API Reference: [ScheduleManyAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %})*

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

*API Reference: [ScheduleAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule.md %}), [ManifestOptions]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %})*

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

*API Reference: [ScheduleManyAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %})*

### Pruning Stale Manifests

When the source collection shrinks between deployments—tables removed, slices reduced—old manifests stick around in the database. The name-based overload handles this automatically (pruning is always enabled via `prunePrefix: "{name}-"`):

```csharp
// If "partner" was removed from this list since the last deployment,
// its manifest (sync-partner) will be deleted because it starts with "sync-"
// but wasn't included in the current batch.
var tables = new[] { "customer", "user" };

scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    "sync",
    tables,
    table => (table, new SyncTableInput { TableName = table }),
    Every.Minutes(5));
```

With the explicit overload, specify `prunePrefix` manually:

```csharp
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    prunePrefix: "sync-");
```

*API Reference: [ScheduleMany]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %})*

When `prunePrefix` is specified, after creating or updating the manifests in the batch, the scheduler deletes any existing manifests whose `ExternalId` starts with the prefix but weren't part of the current call. This keeps the manifest table in sync with your source data without manual cleanup.

## Per-Group Dispatch Controls

Each ManifestGroup has three configurable properties that govern how its manifests are dispatched:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxActiveJobs` | `int?` | `null` (unlimited) | Maximum concurrent active jobs (Pending + InProgress) within this group |
| `Priority` | `int` (0–31) | `0` | Dispatch ordering between groups—higher values are dispatched first |
| `IsEnabled` | `bool` | `true` | When `false`, all manifests in the group are skipped during queuing and dispatch |

These settings are configured from the dashboard's **Manifest Group detail page**, not from code. This keeps scheduling declarations in code focused on *what* runs and *when*, while operators control *how much* and *in what order* from the dashboard.

**MaxActiveJobs** limits concurrent active jobs within a single group. When a group hits its cap, the [JobDispatcher](admin-workflows/job-dispatcher.md) skips it and moves on to the next group—so other groups can still dispatch normally. This prevents a single high-throughput group from monopolizing all capacity. The global `MaxActiveJobs` (configured in code) still applies as an overall ceiling across all groups.

**Priority** determines the order in which groups are considered during dispatch. The JobDispatcher processes groups from highest priority (31) to lowest (0). If a high-priority group continually re-queues work, it is dispatched first—but because `MaxActiveJobs` caps how many jobs it can have active at once, lower-priority groups still get their fair share of capacity. This solves the starvation problem: priority controls *ordering*, while `MaxActiveJobs` controls *capacity*.

**IsEnabled** acts as a kill switch for an entire group. Disabling a group prevents its manifests from being queued or dispatched until re-enabled. This is useful during maintenance windows or when a downstream system is unavailable.

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

*API Reference: [DisableAsync / EnableAsync / TriggerAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/manifest-management.md %}), [ScheduleDependentAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %})*

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

*API Reference: [ScheduleAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule.md %}), [ManifestOptions]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %})*

## Schedule Types

| Type | Use Case | API |
|------|----------|-----|
| `Interval` | Simple recurring | `Every.Minutes(5)` or `Schedule.FromInterval(TimeSpan)` |
| `Cron` | Traditional scheduling | `Cron.Daily()` or `Schedule.FromCron("0 3 * * *")` |
| `Dependent` | Runs after another manifest succeeds | `.ThenInclude()` / `.ThenIncludeMany()` / `.Include()` / `.IncludeMany()` or `ScheduleDependentAsync` |
| `None` | Manual trigger only | Use `scheduler.TriggerAsync(externalId)` |

See [Dependent Workflows](dependent-workflows.md) for details on chaining workflows.

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `PollingInterval` | 5s | How often ManifestManager checks for pending jobs (supports sub-minute) |
| `MaxActiveJobs` | 100 | Maximum active jobs (Pending + InProgress) allowed globally. Enforced by the [JobDispatcher](admin-workflows/job-dispatcher.md) at dispatch time. Set to null for unlimited. Per-group `MaxActiveJobs` can also be set from the dashboard's Manifest Group detail page to limit concurrency within individual groups (see [Per-Group Dispatch Controls](#per-group-dispatch-controls)) |
| `DefaultMaxRetries` | 3 | Retry attempts before dead-lettering |
| `DefaultRetryDelay` | 5m | Delay between retries |
| `RetryBackoffMultiplier` | 2.0 | Exponential backoff (delays of 5m, 10m, 20m...) |
| `MaxRetryDelay` | 1h | Cap on backoff growth |
| `DefaultJobTimeout` | 1h | When a job is considered stuck |
| `RecoverStuckJobsOnStartup` | true | Re-evaluate stuck jobs on startup |

*API Reference: [AddScheduler]({{ site.baseurl }}{% link api-reference/scheduler-api/add-scheduler.md %})*
