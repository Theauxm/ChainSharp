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
    Action<ManifestOptions>? configure = null,
    string? groupId = null,
    int priority = 0
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
    Action<ManifestOptions>? configure = null,
    string? groupId = null,
    int priority = 0,
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
| `configure` | `Action<ManifestOptions>?` | No | `null` | Optional callback to set per-job options like `MaxRetries`, `IsEnabled`, `Timeout`, and `Priority`. See [ManifestOptions]({{ site.baseurl }}{% link api-reference/scheduler-api/scheduling-helpers.md %}#manifestoptions). |
| `groupId` | `string?` | No | `null` | Manifest group name. All manifests with the same groupId share per-group dispatch controls (MaxActiveJobs, Priority, IsEnabled) configurable from the dashboard. When null, defaults to the externalId — each manifest gets its own group. |
| `priority` | `int` | No | `0` | Dispatch priority (0-31). Higher values are dispatched first by the JobDispatcher. Applied before `configure`, so the callback can override. |
| `ct` | `CancellationToken` | No | `default` | Cancellation token (runtime API only). |

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
            externalId: "sync-daily",
            input: new SyncInput { Source = "production" },
            schedule: Cron.Daily(hour: 3),
            configure: opts => opts.MaxRetries = 5,
            groupId: "daily-syncs",
            priority: 20)
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
            externalId: "sync-on-demand",
            input: new SyncInput { Source = "staging" },
            schedule: Every.Hours(1),
            priority: 15);
    }
}
```

## Remarks

- At startup, manifests are not created immediately — they are captured and seeded when the application starts via `ManifestPollingService`.
- At runtime, `ScheduleAsync` creates/updates the manifest in the database immediately.
- The `externalId` is the primary key for upsert logic. Changing it creates a new manifest rather than updating the existing one.
- If the workflow type is not registered in the `WorkflowRegistry` (via `AddEffectWorkflowBus`), an `InvalidOperationException` is thrown.
- The `groupId` determines which ManifestGroup the manifest belongs to. Groups are auto-created (upserted by name) during scheduling. Orphaned groups are cleaned up on startup.
