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
    Action<ManifestOptions>? configure = null
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
| `externalId` | `string` | Yes | — | A unique identifier for this scheduled job. Used for upsert semantics — if a manifest with this ID exists, it will be updated; otherwise a new one is created. Also used to reference this job in dependent scheduling (`Then`). |
| `input` | `TInput` | Yes | — | The input data that will be passed to the workflow on each execution. Serialized and stored in the manifest. |
| `schedule` | `Schedule` | Yes | — | The schedule definition — either interval-based or cron-based. Use [Every]({% link api-reference/scheduler-api/scheduling-helpers.md %}) or [Cron]({% link api-reference/scheduler-api/scheduling-helpers.md %}) helpers to create one. |
| `configure` | `Action<ManifestOptions>?` | No | `null` | Optional callback to set per-job options like `MaxRetries`, `IsEnabled`, and `Timeout`. See [ManifestOptions]({% link api-reference/scheduler-api/scheduling-helpers.md %}#manifestoptions). |
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
            configure: opts => opts.MaxRetries = 5)
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
            configure: opts => opts.Timeout = TimeSpan.FromMinutes(30));
    }
}
```

## Remarks

- At startup, manifests are not created immediately — they are captured and seeded when the application starts via `ManifestPollingService`.
- At runtime, `ScheduleAsync` creates/updates the manifest in the database immediately.
- The `externalId` is the primary key for upsert logic. Changing it creates a new manifest rather than updating the existing one.
- If the workflow type is not registered in the `WorkflowRegistry` (via `AddEffectWorkflowBus`), an `InvalidOperationException` is thrown.
