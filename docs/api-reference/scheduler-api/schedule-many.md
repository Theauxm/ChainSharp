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

### Startup (SchedulerConfigurationBuilder)

```csharp
public SchedulerConfigurationBuilder ScheduleMany<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Schedule schedule,
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null
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
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null,
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

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `sources` | `IEnumerable<TSource>` | Yes | — | The collection of items to create manifests from. Each item becomes one scheduled manifest. |
| `map` | `Func<TSource, (string ExternalId, TInput Input)>` | Yes | — | A function that transforms each source item into an `ExternalId` (unique identifier) and `Input` (workflow input data) pair. |
| `schedule` | `Schedule` | Yes | — | The schedule definition applied to **all** manifests in the batch. Use [Every]({% link api-reference/scheduler-api/scheduling-helpers.md %}) or [Cron]({% link api-reference/scheduler-api/scheduling-helpers.md %}) helpers. |
| `configure` | `Action<TSource, ManifestOptions>?` | No | `null` | Optional callback to set per-item manifest options. Unlike `Schedule`'s `Action<ManifestOptions>`, this receives **both** the source item and the options, allowing per-item configuration. |
| `prunePrefix` | `string?` | No | `null` | When specified, deletes any existing manifests whose `ExternalId` starts with this prefix but were **not** included in the current batch. Enables automatic cleanup when items are removed from the source collection between deployments. |
| `groupId` | `string?` | No | `null` | Optional group identifier for dashboard grouping. All manifests in the batch are tagged with this group. |
| `ct` | `CancellationToken` | No | `default` | Cancellation token (runtime API only). |

## Returns

- **Startup**: `SchedulerConfigurationBuilder` — for continued fluent chaining.
- **Runtime**: `Task<IReadOnlyList<Manifest>>` — all created or updated manifest records.

## Examples

### Basic Batch Scheduling

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

```csharp
// If "partners" was in a previous deployment but removed from this list,
// its manifest ("sync-partners") will be deleted because it starts with
// "sync-" but wasn't included in the current batch.
var tables = new[] { "customers", "orders" };

scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    prunePrefix: "sync-");
```

### With Per-Item Configuration

```csharp
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    configure: (table, opts) =>
    {
        // Give the "orders" table more retries
        if (table == "orders")
            opts.MaxRetries = 10;
    });
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
            prunePrefix: "tenant-sync-",
            groupId: "tenant-syncs");
    }
}
```

## Remarks

- All manifests are created/updated in a **single database transaction**. If any manifest fails to save, the entire batch is rolled back.
- Pruning is included in the same transaction — stale manifests are deleted atomically with the new batch.
- The `configure` callback receives `Action<TSource, ManifestOptions>` (not `Action<ManifestOptions>` like `Schedule`) — this lets you customize options based on the source item.
- The source collection is materialized (`.ToList()`) internally to avoid multiple enumeration.
- `ScheduleMany` cannot be followed by `.Then()` — use [ThenMany]({% link api-reference/scheduler-api/dependent-scheduling.md %}) instead for batch dependent scheduling.
