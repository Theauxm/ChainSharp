---
layout: default
title: Dependent Scheduling
parent: Scheduler API
grand_parent: API Reference
nav_order: 5
---

# Dependent Scheduling

Schedules workflows that run only after a parent manifest completes successfully. There are three patterns for declaring dependencies at startup:

- **Root-based** (`Include` / `IncludeMany`) — branches from the root `Schedule` or explicitly maps parents via `dependsOn`. Use `IncludeMany` for first-level batch dependents after `ScheduleMany`.
- **Cursor-based** (`ThenInclude` / `ThenIncludeMany`) — chains from the most recently declared manifest, creating deeper pipelines. Use `ThenIncludeMany` for second-level-and-beyond batch dependents after a previous `IncludeMany`.

At runtime, `ScheduleDependentAsync` and `ScheduleManyDependentAsync` take an explicit parent external ID.

Dependent manifests are evaluated during polling — when a parent's `LastSuccessfulRun` is newer than the dependent's own `LastSuccessfulRun`, the dependent is queued for execution.

## Signatures

### Startup: ThenInclude (Single — cursor-based)

```csharp
public SchedulerConfigurationBuilder ThenInclude<TWorkflow, TInput>(
    string externalId,
    TInput input,
    Action<ManifestOptions>? configure = null,
    string? groupId = null,
    int priority = 0
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Startup: Include (Single — root-based)

```csharp
public SchedulerConfigurationBuilder Include<TWorkflow, TInput>(
    string externalId,
    TInput input,
    Action<ManifestOptions>? configure = null,
    string? groupId = null,
    int priority = 0
)
    where TWorkflow : IEffectWorkflow<TInput, Unit>
    where TInput : IManifestProperties
```

### Startup: IncludeMany — Name-Based (Recommended)

```csharp
// With explicit dependsOn
public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
    string name,
    IEnumerable<TSource> sources,
    Func<TSource, (string Suffix, TInput Input)> map,
    Func<TSource, string> dependsOn,
    Action<TSource, ManifestOptions>? configure = null,
    int priority = 0
)

// Root-based (no dependsOn) — all items depend on the root Schedule
public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
    string name,
    IEnumerable<TSource> sources,
    Func<TSource, (string Suffix, TInput Input)> map,
    Action<TSource, ManifestOptions>? configure = null,
    int priority = 0
)
```

The `name` parameter automatically derives `groupId` = `name`, `prunePrefix` = `"{name}-"`, and each external ID = `"{name}-{suffix}"`.

### Startup: IncludeMany — Explicit

```csharp
// With explicit dependsOn
public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Func<TSource, string> dependsOn,
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null,
    int priority = 0
)

// Root-based (no dependsOn) — all items depend on the root Schedule
public SchedulerConfigurationBuilder IncludeMany<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null,
    int priority = 0
)
```

### Startup: ThenIncludeMany — Name-Based (Recommended)

```csharp
public SchedulerConfigurationBuilder ThenIncludeMany<TWorkflow, TInput, TSource>(
    string name,
    IEnumerable<TSource> sources,
    Func<TSource, (string Suffix, TInput Input)> map,
    Func<TSource, string> dependsOn,
    Action<TSource, ManifestOptions>? configure = null,
    int priority = 0
)
```

### Startup: ThenIncludeMany — Explicit

```csharp
public SchedulerConfigurationBuilder ThenIncludeMany<TWorkflow, TInput, TSource>(
    IEnumerable<TSource> sources,
    Func<TSource, (string ExternalId, TInput Input)> map,
    Func<TSource, string> dependsOn,
    Action<TSource, ManifestOptions>? configure = null,
    string? prunePrefix = null,
    string? groupId = null,
    int priority = 0
)
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

### ThenInclude / Include (Startup — Single)

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `externalId` | `string` | Yes | Unique identifier for this dependent job |
| `input` | `TInput` | Yes | Input data passed to the workflow on each execution |
| `configure` | `Action<ManifestOptions>?` | No | Per-job options (MaxRetries, Timeout, Priority, etc.) |
| `groupId` | `string?` | No | Manifest group name. Defaults to externalId when null. See [ManifestGroup]({{ site.baseurl }}{% link scheduler/scheduling-options.md %}#per-group-dispatch-controls). |
| `priority` | `int` | No | Base dispatch priority (0-31, default 0). `DependentPriorityBoost` is added on top at dispatch time. `configure` can override. |

`ThenInclude` links to the **cursor** — the most recently declared manifest (the last `Schedule`, `ThenInclude`, or `Include`). Must be called after `Schedule()`, `Include()`, or another `ThenInclude()`.

`Include` links to the **root** — the most recent `Schedule()` call. Must be called after `Schedule()`. Use `Include` to create multiple independent branches from a single root.

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

### IncludeMany / ThenIncludeMany / ScheduleManyDependentAsync (Batch)

The name-based overloads take the same parameters as [ScheduleMany name-based]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %}#name-based-overload), plus:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dependsOn` | `Func<TSource, string>` | Yes | A function that maps each source item to the `ExternalId` of its parent manifest |

The explicit overloads take the same parameters as [ScheduleMany explicit]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %}#explicit-overload), plus `dependsOn`.

`IncludeMany` (with `dependsOn`) is for first-level batch dependents — use after `ScheduleMany` or `Schedule`. `ThenIncludeMany` is for deeper chaining after a previous `IncludeMany`.

### IncludeMany (Batch — root-based, no `dependsOn`)

Same parameters as [ScheduleMany]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %}) (without `schedule` or `dependsOn`) — all items automatically depend on the root `Schedule()`. Must be called after `Schedule()`.

## Examples

### Chained Dependencies (A &rarr; B &rarr; C)

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
        .ThenInclude<ITransformWorkflow, TransformInput>(
            "etl-transform",
            new TransformInput(),
            groupId: "etl-pipeline",
            priority: 5)
        // C: Load runs after Transform succeeds
        .ThenInclude<ILoadWorkflow, LoadInput>(
            "etl-load",
            new LoadInput(),
            groupId: "etl-pipeline")
    )
);
```

### Fan-Out Dependencies (A &rarr; B, A &rarr; C)

Use `Include` to create multiple branches from a single root. Each `Include` depends on the root `Schedule`, not the previous call:

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1),
        groupId: "etl")
    // Both Transform and Validate depend on Extract (fan-out)
    .Include<ITransformWorkflow, TransformInput>(
        "transform", new TransformInput(),
        groupId: "etl")
    .Include<IValidateWorkflow, ValidateInput>(
        "validate", new ValidateInput(),
        groupId: "etl")
```

### Mixed Fan-Out and Chaining

`Include` and `ThenInclude` can be combined. `Include` branches from the root, `ThenInclude` chains from the cursor:

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1),
        groupId: "etl")
    // Branch 1: Extract → Transform → Load
    .Include<ITransformWorkflow, TransformInput>(
        "transform", new TransformInput(),
        groupId: "etl")
        .ThenInclude<ILoadWorkflow, LoadInput>(
            "load", new LoadInput(),
            groupId: "etl")
    // Branch 2: Extract → Validate (back to root)
    .Include<IValidateWorkflow, ValidateInput>(
        "validate", new ValidateInput(),
        groupId: "etl")
```

Result: `Extract → Transform → Load`, `Extract → Validate`

### Batch Dependent Scheduling

```csharp
// Name-based: groupId, prunePrefix, and external ID prefix derived from name
scheduler
    .ScheduleMany<IExtractWorkflow, ExtractInput, int>(
        "extract",
        Enumerable.Range(0, 10),
        i => ($"{i}", new ExtractInput { Index = i }),
        Every.Minutes(5))
    .IncludeMany<ITransformWorkflow, TransformInput, int>(
        "transform",
        Enumerable.Range(0, 10),
        i => ($"{i}", new TransformInput { Index = i }),
        dependsOn: i => $"extract-{i}");
// Creates: extract-0..extract-9, transform-0..transform-9
```

### Batch Fan-Out (IncludeMany)

All items in the batch depend on a single root `Schedule`:

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract-all", new ExtractInput(), Every.Hours(1),
        groupId: "extract")
    .IncludeMany<ILoadWorkflow, LoadInput, int>(
        "load",
        Enumerable.Range(0, 10),
        i => ($"{i}", new LoadInput { Partition = i }))
```

All 10 `load-*` manifests depend on `extract-all`. The name `"load"` auto-derives `groupId: "load"` and `prunePrefix: "load-"`.

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

- `ThenInclude()` must follow `Schedule()`, `Include()`, or another `ThenInclude()` — calling it first throws `InvalidOperationException`.
- `Include()` and `IncludeMany()` (without `dependsOn`) must follow `Schedule()` — calling them without a root throws `InvalidOperationException`.
- `IncludeMany()` (with `dependsOn`) can follow `ScheduleMany()` for first-level batch dependents. `ThenIncludeMany()` is for deeper chaining after a previous `IncludeMany()`.
- Dependent manifests have `ScheduleType.Dependent` and no interval/cron schedule of their own — they are triggered solely by their parent's successful completion.
- The dependency check compares `parent.LastSuccessfulRun > dependent.LastSuccessfulRun` during each polling cycle.
- **Cursor vs. Root**: The builder tracks two pointers — the *cursor* (last declared manifest, used by `ThenInclude`) and the *root* (the last `Schedule()`, used by `Include`). `Schedule` sets both. `ThenInclude` and `Include` move the cursor but leave the root unchanged. `ScheduleMany` resets both to null.
- **Priority boost**: When a dependent manifest's work queue entry is created, `DependentPriorityBoost` (default 16) is added to its base priority. This ensures dependent workflows are dispatched before non-dependent workflows by default. The boost is configurable via [`DependentPriorityBoost`]({{ site.baseurl }}{% link api-reference/scheduler-api/add-scheduler.md %}) on the scheduler builder. The final priority is clamped to [0, 31].
- **Cycle detection**: ManifestGroup dependencies must form a DAG. At startup, the builder derives group-level edges from all `Schedule`/`ThenInclude`/`Include`/`ScheduleMany`/`ThenIncludeMany`/`IncludeMany` calls and validates that no circular dependencies exist between groups. If a cycle is detected, `Build()` throws `InvalidOperationException` listing the groups involved. Within-group dependencies are allowed—only cross-group edges are validated. See [Dependent Workflows — Cycle Detection]({{ site.baseurl }}{% link scheduler/dependent-workflows.md %}#cycle-detection) for details.
