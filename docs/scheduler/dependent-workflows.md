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

## Startup Configuration: Then

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
        .Then<ITransformWorkflow, TransformInput>(
            "transform-data",
            new TransformInput { Format = "parquet" },
            groupId: "etl")
    )
);
```

*API Reference: [Schedule]({% link api-reference/scheduler-api/schedule.md %}), [Then]({% link api-reference/scheduler-api/dependent-scheduling.md %})*

`Then` captures the previous call's external ID as the parent. No schedule parameter—dependent manifests don't have one.

Chaining works: `.Schedule(...).Then(...).Then(...)` creates A &rarr; B &rarr; C. Each `Then` depends on the one before it.

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1),
        groupId: "etl")
    .Then<ITransformWorkflow, TransformInput>(
        "transform", new TransformInput(),
        groupId: "etl")
    .Then<ILoadWorkflow, LoadInput>(
        "load", new LoadInput(),
        groupId: "etl");
```

Here, `transform` runs after `extract` succeeds, and `load` runs after `transform` succeeds. If `extract` fails, neither downstream workflow fires.

## Bulk Dependencies: ThenMany

For batch jobs where each item in one batch depends on a corresponding item in another, use `ThenMany` after `ScheduleMany`:

```csharp
scheduler
    .ScheduleMany<IExtractWorkflow, ExtractInput, int>(
        Enumerable.Range(0, 100),
        i => ($"extract-{i}", new ExtractInput { Partition = i }),
        Every.Minutes(30),
        prunePrefix: "extract-",
        groupId: "extract")
    .ThenMany<ILoadWorkflow, LoadInput, int>(
        Enumerable.Range(0, 100),
        i => ($"load-{i}", new LoadInput { Partition = i }),
        dependsOn: i => $"extract-{i}",
        prunePrefix: "load-",
        groupId: "load");
```

*API Reference: [ScheduleMany]({% link api-reference/scheduler-api/schedule-many.md %}), [ThenMany]({% link api-reference/scheduler-api/dependent-scheduling.md %})*

The `dependsOn` function maps each source item to its parent's external ID. In this example, `load-0` depends on `extract-0`, `load-1` on `extract-1`, and so on. When `extract-42` succeeds, only `load-42` gets queued—the rest are unaffected.

The mapping is flexible. You aren't limited to 1:1. Multiple dependents can point to the same parent:

```csharp
// All load jobs depend on a single extract job
.ThenMany<ILoadWorkflow, LoadInput, int>(
    Enumerable.Range(0, 10),
    i => ($"load-{i}", new LoadInput { Partition = i }),
    dependsOn: _ => "extract-all");
```

`ThenMany` supports `prunePrefix` and `groupId` just like `ScheduleMany`. When a chain shares a `groupId`, all manifests belong to the same ManifestGroup, sharing per-group dispatch controls.

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

*API Reference: [ScheduleDependentAsync]({% link api-reference/scheduler-api/dependent-scheduling.md %}), [ScheduleManyDependentAsync]({% link api-reference/scheduler-api/dependent-scheduling.md %})*

Both methods use upsert semantics, same as their non-dependent counterparts. `ScheduleManyDependentAsync` runs in a single transaction.

## Cycle Detection

ManifestGroup dependencies must form a directed acyclic graph (DAG). Circular dependencies between groups—where group A depends on group B which depends back on group A, directly or transitively—would create a deadlock where no group can ever fire.

The scheduler validates this **at startup**. When `AddScheduler` builds the configuration, it derives group-level edges from the manifest-level `Schedule`/`Then`/`ScheduleMany`/`ThenMany` calls and runs a topological sort (Kahn's algorithm) over the group graph. If a cycle is detected, the application fails fast with an `InvalidOperationException` listing the groups involved:

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
