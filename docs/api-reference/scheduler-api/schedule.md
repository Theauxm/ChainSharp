---
layout: default
title: Schedule / ScheduleAsync
parent: Scheduler API
grand_parent: API Reference
nav_order: 3
---

# Schedule / ScheduleAsync

Schedules a single workflow to run on a recurring basis. `Schedule` is used at startup configuration time; `ScheduleAsync` is used at runtime via `IManifestScheduler`.

Both use **upsert semantics** — if a manifest with the given `externalId` already exists, it is updated; otherwise a new one is created.

## Signatures

### Startup (SchedulerConfigurationBuilder)

```csharp
public SchedulerConfigurationBuilder Schedule<TWorkflow, TInput>(
    string externalId,
    TInput input,
    Schedule schedule,
    Action<ScheduleOptions>? options = null
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Runtime (IManifestScheduler)

```csharp
Task<Manifest> ScheduleAsync<TWorkflow, TInput>(
    string externalId,
    TInput input,
    Schedule schedule,
    Action<ScheduleOptions>? options = null,
    CancellationToken ct = default
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

## Type Parameters

| Type Parameter | Constraint | Description |
|---------------|------------|-------------|
| `TWorkflow` | `IEffectWorkflow<TInput, Unit>` | The workflow interface type. The scheduler resolves the concrete implementation via `WorkflowBus` using the input type. Must return `Unit` (no return value). |
| `TInput` | `IManifestProperties` | The input type for the workflow. Must implement `IManifestProperties` (a marker interface) to enable serialization for scheduled job storage. |

## Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `externalId` | `string` | Yes | — | A unique identifier for this scheduled job. Used for upsert semantics — if a manifest with this ID exists, it will be updated; otherwise a new one is created. Also used to reference this job in dependent scheduling (`ThenInclude`, `Include`). |
| `input` | `TInput` | Yes | — | The input data that will be passed to the workflow on each execution. Serialized and stored in the manifest. |
| `schedule` | `Schedule` | Yes | — | The schedule definition — either interval-based or cron-based. Use [Every]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}) or [Cron]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}) helpers to create one. |
| `options` | `Action<ScheduleOptions>?` | No | `null` | Optional callback to configure all scheduling options via a fluent builder. Includes manifest-level settings (`Priority`, `Enabled`, `MaxRetries`, `Timeout`), group-level settings (`.Group(...)` with `MaxActiveJobs`, `Priority`, `Enabled`), and batch settings (`PrunePrefix`). See [ScheduleOptions](#scheduleoptions) below. |
| `ct` | `CancellationToken` | No | `default` | Cancellation token (runtime API only). |

## ScheduleOptions

The `ScheduleOptions` fluent builder consolidates all optional scheduling parameters:

### Manifest-level methods

| Method | Description |
|--------|-------------|
| `.Priority(int)` | Dispatch priority (0-31). Higher values dispatched first. |
| `.Enabled(bool)` | Whether the manifest is enabled. Default: `true`. |
| `.MaxRetries(int)` | Max retry attempts before dead-lettering. Default: `3`. |
| `.Timeout(TimeSpan)` | Job execution timeout. `null` uses global default. |

### Group-level methods

| Method | Description |
|--------|-------------|
| `.Group(string groupId)` | Sets the manifest group name. Defaults to `externalId` when not set. |
| `.Group(string groupId, Action<ManifestGroupOptions>)` | Sets group name and configures group dispatch settings. |
| `.Group(Action<ManifestGroupOptions>)` | Configures group dispatch settings without changing the group name. |

### ManifestGroupOptions

| Method | Description |
|--------|-------------|
| `.MaxActiveJobs(int?)` | Max concurrent active jobs for this group. `null` = no per-group limit. |
| `.Priority(int)` | Group dispatch priority (0-31). Defaults to manifest priority if not set. |
| `.Enabled(bool)` | Kill switch for the entire group. Default: `true`. |

## Returns

- **Startup**: `SchedulerConfigurationBuilder` — for continued fluent chaining.
- **Runtime**: `Task<Manifest>` — the created or updated manifest record.

## Examples

### Startup Configuration

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .Schedule<ISyncWorkflow, SyncInput>(
            "sync-daily",
            new SyncInput { Source = "production" },
            Cron.Daily(hour: 3),
            options => options
                .MaxRetries(5)
                .Priority(20)
                .Group("daily-syncs"))
    )
);
```

### Runtime Scheduling

```csharp
public class MyService(IManifestScheduler scheduler)
{
    public async Task SetupSchedule()
    {
        var manifest = await scheduler.ScheduleAsync<ISyncWorkflow, SyncInput>(
            "sync-on-demand",
            new SyncInput { Source = "staging" },
            Every.Hours(1),
            options => options.Priority(15));
    }
}
```

## Remarks

- At startup, manifests are not created immediately — they are captured and seeded when the application starts via `SchedulerStartupService`.
- At runtime, `ScheduleAsync` creates/updates the manifest in the database immediately.
- The `externalId` is the primary key for upsert logic. Changing it creates a new manifest rather than updating the existing one.
- If the workflow type is not registered in the `WorkflowRegistry` (via `AddEffectWorkflowBus`), an `InvalidOperationException` is thrown.
- The group is determined by `.Group(...)` on `ScheduleOptions`. When not specified, it defaults to the `externalId`. Groups are auto-created (upserted by name) during scheduling. Orphaned groups are cleaned up on startup.
