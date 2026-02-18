---
layout: default
title: Dependent Scheduling
parent: Scheduler API
grand_parent: API Reference
nav_order: 5
---

# Dependent Scheduling

Schedules workflows that run only after a parent manifest completes successfully. Supports both single (`Then` / `ScheduleDependentAsync`) and batch (`ThenMany` / `ScheduleManyDependentAsync`) dependent scheduling.

Dependent manifests are evaluated during polling — when a parent's `LastSuccessfulRun` is newer than the dependent's own `LastSuccessfulRun`, the dependent is queued for execution.

## Signatures

### Startup: Then (Single)

```csharp
public SchedulerConfigurationBuilder Then<TWorkflow, TInput>(
    string externalId,
    TInput input,
    Action<ManifestOptions>? configure = null,
    string? groupId = null,
    int priority = 0
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Startup: ThenMany (Batch)

```csharp
public SchedulerConfigurationBuilder ThenMany<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Func<TSource, string> dependsOn,
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null,
    int priority = 0
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Runtime: ScheduleDependentAsync (Single)

```csharp
Task<Manifest> ScheduleDependentAsync<TWorkflow, TInput>(
    string externalId,
    TInput input,
    string dependsOnExternalId,
    Action<ManifestOptions>? configure = null,
    string? groupId = null,
    int priority = 0,
    CancellationToken ct = default
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Runtime: ScheduleManyDependentAsync (Batch)

```csharp
Task<IReadOnlyList<Manifest>> ScheduleManyDependentAsync<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Func<TSource, string> dependsOn,
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null,
    int priority = 0,
    CancellationToken ct = default
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

## Parameters

### Then (Startup)

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `externalId` | `string` | Yes | Unique identifier for this dependent job |
| `input` | `TInput` | Yes | Input data passed to the workflow on each execution |
| `configure` | `Action<ManifestOptions>?` | No | Per-job options (MaxRetries, Timeout, Priority, etc.) |
| `groupId` | `string?` | No | Manifest group name. Defaults to externalId when null. See [ManifestGroup]({% link scheduler/scheduling-options.md %}#per-group-dispatch-controls). |
| `priority` | `int` | No | Base dispatch priority (0-31, default 0). `DependentPriorityBoost` is added on top at dispatch time. `configure` can override. |

`Then` **implicitly** links to the previously scheduled manifest. Must be called after `Schedule()` or another `Then()`.

### ScheduleDependentAsync (Runtime)

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `externalId` | `string` | Yes | Unique identifier for this dependent job |
| `input` | `TInput` | Yes | Input data passed to the workflow on each execution |
| `dependsOnExternalId` | `string` | Yes | The `ExternalId` of the parent manifest this job depends on |
| `configure` | `Action<ManifestOptions>?` | No | Per-job options |
| `groupId` | `string?` | No | Manifest group name. Defaults to externalId when null. |
| `priority` | `int` | No | Base dispatch priority (0-31, default 0). `DependentPriorityBoost` is added on top at dispatch time. `configure` can override. |
| `ct` | `CancellationToken` | No | Cancellation token |

### ThenMany / ScheduleManyDependentAsync (Batch)

All parameters from [ScheduleMany]({% link api-reference/scheduler-api/schedule-many.md %}), plus:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dependsOn` | `Func<TSource, string>` | Yes | A function that maps each source item to the `ExternalId` of its parent manifest |

## Examples

### Chained Dependencies (A -> B -> C)

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        // A: Extract runs every 5 minutes
        .Schedule<IExtractWorkflow, ExtractInput>(
            "etl-extract",
            new ExtractInput(),
            Every.Minutes(5),
            groupId: "etl-pipeline")
        // B: Transform runs after Extract succeeds (priority 5 + DependentPriorityBoost)
        .Then<ITransformWorkflow, TransformInput>(
            "etl-transform",
            new TransformInput(),
            groupId: "etl-pipeline",
            priority: 5)
        // C: Load runs after Transform succeeds
        .Then<ILoadWorkflow, LoadInput>(
            "etl-load",
            new LoadInput(),
            groupId: "etl-pipeline")
    )
);
```

### Batch Dependent Scheduling

```csharp
scheduler
    .ScheduleMany<IExtractWorkflow, ExtractInput, int>(
        Enumerable.Range(0, 10),
        i => ($"extract-{i}", new ExtractInput { Index = i }),
        Every.Minutes(5),
        groupId: "extract")
    .ThenMany<ITransformWorkflow, TransformInput, int>(
        Enumerable.Range(0, 10),
        i => ($"transform-{i}", new TransformInput { Index = i }),
        dependsOn: i => $"extract-{i}",
        groupId: "transform");
```

### Runtime Dependent Scheduling

```csharp
// Create parent
await scheduler.ScheduleAsync<IFetchDataWorkflow, FetchInput>(
    "fetch-data", new FetchInput(), Cron.Hourly());

// Create dependent
await scheduler.ScheduleDependentAsync<IProcessDataWorkflow, ProcessInput>(
    externalId: "process-data",
    input: new ProcessInput(),
    dependsOnExternalId: "fetch-data");
```

## Remarks

- `Then()` must follow `Schedule()` or another `Then()` — calling it first throws `InvalidOperationException`.
- `ThenMany()` cannot follow `ScheduleMany()` directly via implicit chaining — each item needs an explicit `dependsOn` function to map to its parent's `ExternalId`.
- Dependent manifests have `ScheduleType.Dependent` and no interval/cron schedule of their own — they are triggered solely by their parent's successful completion.
- The dependency check compares `parent.LastSuccessfulRun > dependent.LastSuccessfulRun` during each polling cycle.
- **Priority boost**: When a dependent manifest's work queue entry is created, `DependentPriorityBoost` (default 16) is added to its base priority. This ensures dependent workflows are dispatched before non-dependent workflows by default. The boost is configurable via [`DependentPriorityBoost`]({% link api-reference/scheduler-api/add-scheduler.md %}) on the scheduler builder. The final priority is clamped to [0, 31].
