---
layout: default
title: ScheduleMany / ScheduleManyAsync
parent: Scheduler API
grand_parent: API Reference
nav_order: 4
---

# ScheduleMany / ScheduleManyAsync

Batch-schedules multiple instances of a workflow from a collection. All manifests are created or updated in a **single transaction**. Supports automatic cleanup of stale manifests via `prunePrefix`.

`ScheduleMany` is used at startup configuration time; `ScheduleManyAsync` is used at runtime via `IManifestScheduler`.

## Signatures

### Startup: Name-Based (Recommended)

```csharp
public SchedulerConfigurationBuilder ScheduleMany<TWorkflow, TInput, TSource>(
    string name,
    IEnumerable<TSource> sources,
    Func<TSource, (string Suffix, TInput Input)> map,
    Schedule schedule,
    Action<ScheduleOptions>? options = null,
    Action<TSource, ManifestOptions>? configureEach = null
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

The `name` parameter automatically derives:
- **`groupId`** = `name`
- **`prunePrefix`** = `"{name}-"`
- **`externalId`** = `"{name}-{suffix}"` (where `suffix` comes from the `map` function)

### Startup: Explicit

```csharp
public SchedulerConfigurationBuilder ScheduleMany<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Schedule schedule,
    Action<ScheduleOptions>? options = null,
    Action<TSource, ManifestOptions>? configureEach = null
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Runtime (IManifestScheduler)

```csharp
Task<IReadOnlyList<Manifest>> ScheduleManyAsync<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Schedule schedule,
    Action<ScheduleOptions>? options = null,
    Action<TSource, ManifestOptions>? configureEach = null,
    CancellationToken ct = default
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

## Type Parameters

| Type Parameter | Constraint | Description |
|---------------|------------|-------------|
| `TWorkflow` | `IEffectWorkflow<TInput, Unit>` | The workflow interface type. All items in the batch execute the same workflow. |
| `TInput` | `IManifestProperties` | The input type for the workflow. Each item in the batch can have different input data. |
| `TSource` | — | The type of elements in the source collection. Can be any type — it is transformed into `(ExternalId, Input)` pairs by the `map` function. |

## Parameters

### Name-Based Overload

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `name` | `string` | Yes | — | The batch name. Automatically derives `groupId` = `name`, `prunePrefix` = `"{name}-"`, and each external ID = `"{name}-{suffix}"`. |
| `sources` | `IEnumerable<TSource>` | Yes | — | The collection of items to create manifests from. Each item becomes one scheduled manifest. |
| `map` | `Func<TSource, (string Suffix, TInput Input)>` | Yes | — | A function that transforms each source item into a `Suffix` and `Input` pair. The full external ID is `"{name}-{suffix}"`. |
| `schedule` | `Schedule` | Yes | — | The schedule definition applied to **all** manifests in the batch. Use [Every]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}) or [Cron]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}) helpers. |
| `options` | `Action<ScheduleOptions>?` | No | `null` | Optional callback to configure all scheduling options via a fluent builder. Includes manifest-level settings (`Priority`, `Enabled`, `MaxRetries`, `Timeout`), group-level settings (`.Group(...)` with `MaxActiveJobs`, `Priority`, `Enabled`), and batch settings (`PrunePrefix`). Note: the name-based overload pre-sets `Group(name)` and `PrunePrefix("{name}-")` before invoking your callback, so you can override these if needed. See [ScheduleOptions]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule.md %}#scheduleoptions). |
| `configureEach` | `Action<TSource, ManifestOptions>?` | No | `null` | Optional callback to set per-item manifest options. Receives both the source item and options, allowing per-item overrides of the base options set via `options`. |

### Explicit Overload

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `sources` | `IEnumerable<TSource>` | Yes | — | The collection of items to create manifests from. Each item becomes one scheduled manifest. |
| `map` | `Func<TSource, (string ExternalId, TInput Input)>` | Yes | — | A function that transforms each source item into an `ExternalId` (unique identifier) and `Input` (workflow input data) pair. |
| `schedule` | `Schedule` | Yes | — | The schedule definition applied to **all** manifests in the batch. Use [Every]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}) or [Cron]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}) helpers. |
| `options` | `Action<ScheduleOptions>?` | No | `null` | Optional callback to configure all scheduling options via a fluent builder. Includes manifest-level settings (`Priority`, `Enabled`, `MaxRetries`, `Timeout`), group-level settings (`.Group(...)` with `MaxActiveJobs`, `Priority`, `Enabled`), batch settings (`PrunePrefix`). See [ScheduleOptions]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule.md %}#scheduleoptions). |
| `configureEach` | `Action<TSource, ManifestOptions>?` | No | `null` | Optional callback to set per-item manifest options. Receives **both** the source item and the options, allowing per-item overrides of the base options. |
| `ct` | `CancellationToken` | No | `default` | Cancellation token (runtime API only). |

## Returns

- **Startup**: `SchedulerConfigurationBuilder` — for continued fluent chaining.
- **Runtime**: `Task<IReadOnlyList<Manifest>>` — all created or updated manifest records.

## Examples

### Basic Batch Scheduling (Name-Based)

```csharp
var tables = new[] { "customers", "orders", "products" };

services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            "sync",
            tables,
            table => (table, new SyncTableInput { TableName = table }),
            Every.Minutes(5))
    )
);
// Creates: sync-customers, sync-orders, sync-products
// groupId: "sync", prunePrefix: "sync-"
```

### Basic Batch Scheduling (Explicit)

```csharp
var tables = new[] { "customers", "orders", "products" };

services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
            sources: tables,
            map: table => (
                ExternalId: $"sync-{table}",
                Input: new SyncTableInput { TableName = table }
            ),
            schedule: Every.Minutes(5))
    )
);
```

### With Pruning (Automatic Stale Cleanup)

The name-based overload includes pruning automatically (`prunePrefix: "{name}-"`). With the explicit overload, specify it via `ScheduleOptions`:

```csharp
// If "partners" was in a previous deployment but removed from this list,
// its manifest ("sync-partners") will be deleted because it starts with
// "sync-" but wasn't included in the current batch.
var tables = new[] { "customers", "orders" };

// Name-based: pruning is automatic
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    "sync",
    tables,
    table => (table, new SyncTableInput { TableName = table }),
    Every.Minutes(5));

// Explicit: specify prunePrefix via options
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    options => options.PrunePrefix("sync-"));
```

### With Per-Item Configuration

```csharp
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    options => options.Priority(10),  // Base priority for all items
    configureEach: (table, opts) =>
    {
        // Give the "orders" table more retries and higher priority
        if (table == "orders")
        {
            opts.MaxRetries = 10;
            opts.Priority = 25;
        }
    });
```

### With Group Configuration

```csharp
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    "sync",
    tables,
    table => (table, new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    options => options
        .Priority(10)
        .Group(group => group
            .MaxActiveJobs(5)
            .Priority(20)));
```

### Runtime Scheduling

```csharp
public class TenantSyncService(IManifestScheduler scheduler)
{
    public async Task SyncTenants(IEnumerable<Tenant> tenants)
    {
        var manifests = await scheduler.ScheduleManyAsync<ISyncTenantWorkflow, SyncTenantInput, Tenant>(
            sources: tenants,
            map: tenant => (
                ExternalId: $"tenant-sync-{tenant.Id}",
                Input: new SyncTenantInput { TenantId = tenant.Id, Name = tenant.Name }
            ),
            schedule: Every.Hours(1),
            options => options
                .PrunePrefix("tenant-sync-")
                .Group("tenant-syncs"));
    }
}
```

## Remarks

- All manifests are created/updated in a **single database transaction**. If any manifest fails to save, the entire batch is rolled back.
- Pruning is included in the same transaction — stale manifests are deleted atomically with the new batch.
- The `configureEach` callback receives `Action<TSource, ManifestOptions>` (not `Action<ManifestOptions>` like `Schedule`) — this lets you customize options based on the source item. It applies per-item overrides on top of the base options from `ScheduleOptions`.
- The source collection is materialized (`.ToList()`) internally to avoid multiple enumeration.
- The group is configured via `.Group(...)` on `ScheduleOptions`. Per-group settings (MaxActiveJobs, Priority, IsEnabled) can be set from code or adjusted at runtime from the dashboard. See [Per-Group Dispatch Controls]({{ site.baseurl }}{% link scheduler/scheduling-options.md %}#per-group-dispatch-controls).
- `ScheduleMany` cannot be followed by `.ThenInclude()` — use [IncludeMany]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %}) (with `dependsOn`) instead for batch dependent scheduling.
