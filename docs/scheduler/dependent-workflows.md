---
layout: default
title: Dependent Workflows
parent: Scheduling
nav_order: 4
---

# Dependent Workflows

## The Problem

Some jobs only make sense after another job finishes. An ETL pipeline extracts data first, then transforms and loads it. A notification workflow runs after a report completes. You could schedule both on the same interval and hope the timing works out, but that's fragile—if the parent runs slow or retries, the dependent kicks off against stale data.

Dependent workflows solve this. A manifest with `ScheduleType.Dependent` doesn't run on a timer. It runs when its parent's `LastSuccessfulRun` moves forward.

## How It Works

Each polling cycle, the `ManifestManagerWorkflow` evaluates dependent manifests separately from time-based ones. The logic is simple:

1. Find the parent manifest (via `DependsOnManifestId`)
2. If `parent.LastSuccessfulRun > dependent.LastSuccessfulRun`, queue the dependent
3. If the parent has never succeeded, or the dependent already ran after the parent's last success, skip it

That's it. No event bus, no callbacks. The existing polling loop picks up the change on its next cycle.

The same guards that apply to scheduled manifests apply here too: if the dependent has an active execution, a queued work entry, or a dead letter awaiting intervention, it won't be queued again.

## Startup Configuration: ThenInclude

Chain a dependent workflow after a `Schedule` call:

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .Schedule<IExtractWorkflow, ExtractInput>(
            "extract-data",
            new ExtractInput { Source = "api" },
            Every.Hours(1),
            groupId: "etl")
        .ThenInclude<ITransformWorkflow, TransformInput>(
            "transform-data",
            new TransformInput { Format = "parquet" },
            groupId: "etl")
    )
);
```

*API Reference: [Schedule]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule.md %}), [ThenInclude]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %})*

`ThenInclude` captures the previous call's external ID as the parent. No schedule parameter—dependent manifests don't have one.

Chaining works: `.Schedule(...).ThenInclude(...).ThenInclude(...)` creates A &rarr; B &rarr; C. Each `ThenInclude` depends on the one before it.

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1),
        groupId: "etl")
    .ThenInclude<ITransformWorkflow, TransformInput>(
        "transform", new TransformInput(),
        groupId: "etl")
    .ThenInclude<ILoadWorkflow, LoadInput>(
        "load", new LoadInput(),
        groupId: "etl");
```

Here, `transform` runs after `extract` succeeds, and `load` runs after `transform` succeeds. If `extract` fails, neither downstream workflow fires.

## Fan-Out: Include

Sometimes one job needs to trigger multiple independent downstream jobs. An extract might feed both a transform pipeline and a validation step. `ThenInclude` can't express this — it always chains from the previous manifest, producing a linear pipeline.

`Include` solves this. It always branches from the **root** `Schedule`, not the cursor:

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1),
        groupId: "etl")
    // Both depend on Extract, not on each other
    .Include<ITransformWorkflow, TransformInput>(
        "transform", new TransformInput(),
        groupId: "etl")
    .Include<IValidateWorkflow, ValidateInput>(
        "validate", new ValidateInput(),
        groupId: "etl");
```

*API Reference: [Include]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %})*

This creates: `extract` &rarr; `transform`, `extract` &rarr; `validate`. When extract succeeds, both transform and validate are queued independently. If transform fails, validate is unaffected.

### Mixing Include and ThenInclude

`Include` and `ThenInclude` compose naturally. `Include` branches from the root, `ThenInclude` chains from wherever you are:

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1),
        groupId: "etl")
    // Branch 1: extract → transform → load
    .Include<ITransformWorkflow, TransformInput>(
        "transform", new TransformInput(),
        groupId: "etl")
        .ThenInclude<ILoadWorkflow, LoadInput>(
            "load", new LoadInput(),
            groupId: "etl")
    // Branch 2: extract → validate (back to root)
    .Include<IValidateWorkflow, ValidateInput>(
        "validate", new ValidateInput(),
        groupId: "etl");
```

The builder tracks two pointers: the **root** (set by `Schedule`) and the **cursor** (moved by every `ThenInclude` or `Include`). `Include` always parents from the root. `ThenInclude` always parents from the cursor.

## Bulk Dependencies: IncludeMany

For batch jobs where each item in one batch depends on a corresponding item in another, use `IncludeMany` (with `dependsOn`) after `ScheduleMany`. The name-based overloads eliminate repetitive `groupId`, `prunePrefix`, and external ID prefix strings:

```csharp
scheduler
    .ScheduleMany<IExtractWorkflow, ExtractInput, int>(
        "extract",
        Enumerable.Range(0, 100),
        i => ($"{i}", new ExtractInput { Partition = i }),
        Every.Minutes(30))
    .IncludeMany<ILoadWorkflow, LoadInput, int>(
        "load",
        Enumerable.Range(0, 100),
        i => ($"{i}", new LoadInput { Partition = i }),
        dependsOn: i => $"extract-{i}");
// Creates: extract-0..extract-99 (groupId: "extract", prunePrefix: "extract-")
//          load-0..load-99 (groupId: "load", prunePrefix: "load-")
```

*API Reference: [ScheduleMany]({{ site.baseurl }}{% link api-reference/scheduler-api/schedule-many.md %}), [IncludeMany]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %})*

The `dependsOn` function maps each source item to its parent's external ID. In this example, `load-0` depends on `extract-0`, `load-1` on `extract-1`, and so on. When `extract-42` succeeds, only `load-42` gets queued—the rest are unaffected.

The mapping is flexible. You aren't limited to 1:1. Multiple dependents can point to the same parent:

```csharp
// All load jobs depend on a single extract job
.IncludeMany<ILoadWorkflow, LoadInput, int>(
    "load",
    Enumerable.Range(0, 10),
    i => ($"{i}", new LoadInput { Partition = i }),
    dependsOn: _ => "extract-all");
```

The name-based overloads automatically set `groupId` and `prunePrefix` from the `name` parameter. When a chain shares a `groupId`, all manifests belong to the same ManifestGroup, sharing per-group dispatch controls. The explicit overloads (without the `name` parameter) are still available when you need full control over `groupId`, `prunePrefix`, and external IDs independently.

For deeper chaining (a third batch level), use `ThenIncludeMany`:

```csharp
scheduler
    .ScheduleMany<IExtractWorkflow, ExtractInput, int>(
        "extract",
        Enumerable.Range(0, 10),
        i => ($"{i}", new ExtractInput { Partition = i }),
        Every.Minutes(30))
    .IncludeMany<ITransformWorkflow, TransformInput, int>(
        "transform",
        Enumerable.Range(0, 10),
        i => ($"{i}", new TransformInput { Partition = i }),
        dependsOn: i => $"extract-{i}")
    .ThenIncludeMany<ILoadWorkflow, LoadInput, int>(
        "load",
        Enumerable.Range(0, 10),
        i => ($"{i}", new LoadInput { Partition = i }),
        dependsOn: i => $"transform-{i}");
```

## Bulk Fan-Out: IncludeMany

When a single root manifest should trigger an entire batch of dependents, use `IncludeMany`:

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract-all",
        new ExtractInput { Mode = "full" },
        Every.Hours(1),
        groupId: "extract")
    .IncludeMany<ILoadWorkflow, LoadInput, int>(
        "load",
        Enumerable.Range(0, 10),
        i => ($"{i}", new LoadInput { Partition = i }));
```

*API Reference: [IncludeMany]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %})*

All 10 `load-*` manifests depend on `extract-all`. No `dependsOn` function needed — `IncludeMany` automatically parents every item from the root `Schedule`. The name `"load"` derives `groupId: "load"` and `prunePrefix: "load-"`.

## Runtime API

For jobs created at runtime rather than startup, use `IManifestScheduler` directly:

```csharp
// Single dependent
await scheduler.ScheduleDependentAsync<ILoadWorkflow, LoadInput>(
    "load-customers",
    new LoadInput { Table = "customers" },
    dependsOnExternalId: "extract-customers");

// Batch dependent
await scheduler.ScheduleManyDependentAsync<ILoadWorkflow, LoadInput, string>(
    tables,
    table => ($"load-{table}", new LoadInput { Table = table }),
    dependsOn: table => $"extract-{table}",
    prunePrefix: "load-",
    groupId: "load");
```

*API Reference: [ScheduleDependentAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %}), [ScheduleManyDependentAsync]({{ site.baseurl }}{% link api-reference/scheduler-api/dependent-scheduling.md %})*

Both methods use upsert semantics, same as their non-dependent counterparts. `ScheduleManyDependentAsync` runs in a single transaction.

## Cycle Detection

ManifestGroup dependencies must form a directed acyclic graph (DAG). Circular dependencies between groups—where group A depends on group B which depends back on group A, directly or transitively—would create a deadlock where no group can ever fire.

The scheduler validates this **at startup**. When `AddScheduler` builds the configuration, it derives group-level edges from the manifest-level `Schedule`/`ThenInclude`/`Include`/`ScheduleMany`/`ThenIncludeMany`/`IncludeMany` calls and runs a topological sort (Kahn's algorithm) over the group graph. If a cycle is detected, the application fails fast with an `InvalidOperationException` listing the groups involved:

```
Circular dependency detected among manifest groups: [group-a, group-b, group-c].
Manifest groups must form a directed acyclic graph (DAG).
```

Dependencies **within** a single group (two manifests in the same `groupId` where one depends on the other) are fine—only cross-group edges are checked, since within-group ordering is handled by the polling loop's parent/dependent evaluation.

The dashboard also visualizes the group dependency graph on the ManifestGroups page and each ManifestGroup detail page, making it easy to see the structure at a glance.

## Under the Hood

### Database

Dependent workflows add one column to the `manifest` table:

```sql
ALTER TABLE chain_sharp.manifest
    ADD COLUMN depends_on_manifest_id int
    REFERENCES chain_sharp.manifest(id) ON DELETE SET NULL;
```

It's a self-referencing FK. If the parent manifest is deleted, the dependent's `DependsOnManifestId` is set to `NULL`—it won't fire, but it won't break either.

The `schedule_type` enum gets a new value: `dependent`. Dependent manifests have no `CronExpression` or `IntervalSeconds`—those fields are `NULL`.

### Evaluation in ManifestManagerWorkflow

The `DetermineJobsToQueueStep` runs two passes:

1. **Time-based manifests** (Cron, Interval): checked against their schedule as before
2. **Dependent manifests**: checked against their parent's `LastSuccessfulRun`

The dependent pass loads all enabled manifests (parents and dependents together), so it can resolve parent references without extra queries. If a parent is disabled or missing from the loaded set, its dependents are skipped.

### Chain Behavior

In an A &rarr; B &rarr; C chain, B won't fire until A succeeds. C won't fire until B succeeds. If A succeeds but B fails and gets dead-lettered, C stays idle—it's still waiting for B's `LastSuccessfulRun` to advance.

Each link in the chain is independent. The scheduler doesn't have a concept of "the whole chain failed." Each manifest manages its own retries and dead letters. This keeps the model simple: every manifest is still just a manifest, whether it runs on a timer or after another manifest.

### What Happens When...

| Scenario | Result |
|----------|--------|
| Parent succeeds, dependent never ran | Dependent queued |
| Parent succeeds, dependent already ran after parent's last success | Dependent skipped |
| Parent never succeeded | Dependent skipped |
| Parent disabled | Dependent skipped (parent not in loaded set) |
| Dependent has a dead letter | Dependent skipped until resolved |
| Dependent has an active execution | Dependent skipped (no double-queue) |
| Parent deleted | `DependsOnManifestId` set to NULL, dependent skipped |
