---
layout: default
title: JobDispatcher
parent: Administrative Workflows
grand_parent: Scheduling
nav_order: 2
---

# JobDispatcherWorkflow

The JobDispatcher is the single gateway between the work queue and the background task server. It reads `Queued` entries, enforces both global and per-group `MaxActiveJobs` limits, creates Metadata records, and enqueues to Hangfire (or whatever `IBackgroundTaskServer` implementation is configured).

## Chain

```
LoadQueuedJobs → DispatchJobs
```

## Steps

### LoadQueuedJobsStep

Loads all `WorkQueue` entries with `Status = Queued`, filtering out entries whose `ManifestGroup` has `IsEnabled = false`. The results are ordered by three keys:

1. **ManifestGroup.Priority** (descending) — higher priority groups are dispatched first.
2. **WorkQueue.Priority** (descending) — within a group, higher priority entries come first.
3. **CreatedAt** (ascending) — FIFO tiebreaker within the same priority.

This replaces the previous dependent-first ordering with a fully configurable priority system.

### DispatchJobsStep

The core of the dispatcher. It enforces capacity at two levels before processing entries:

1. **Global capacity check** (if global `MaxActiveJobs` is configured): counts all non-excluded `Pending` or `InProgress` metadata in the database. If the count meets or exceeds the global limit, the entire cycle is skipped—no entries are dispatched.

2. **Per-group capacity check**: for each queued entry, the dispatcher checks whether the entry's `ManifestGroup` has hit its own `MaxActiveJobs` limit. If the group is at capacity, that entry is skipped with `continue` and the loop moves on to the next entry. This is the key starvation fix—a full group doesn't block entries from other groups.

For each entry that passes both capacity checks, the dispatcher:

1. **Deserializes the input**: uses `InputTypeName` to resolve the CLR type, then deserializes `Input` from JSON. Type resolution searches all loaded assemblies.

2. **Creates a Metadata record**: a new `Metadata` row with `WorkflowState = Pending`, linked to the manifest (if present). Saved immediately so it gets a database-generated ID.

3. **Updates the work queue entry**: sets `Status = Dispatched`, records the `MetadataId` and `DispatchedAt` timestamp.

4. **Enqueues to the background task server**: calls `IBackgroundTaskServer.EnqueueAsync` with the metadata ID and deserialized input. This is what hands the job to Hangfire.

If any individual entry fails during dispatch (type resolution, serialization, database error), the error is logged and the loop continues to the next entry. One bad entry doesn't block the rest of the queue.

## MaxActiveJobs Enforcement

Capacity is enforced at two independent levels: global and per-group.

### Global MaxActiveJobs

The global limit works the same as before. The count is based on `Metadata` rows in `Pending` or `InProgress` state, excluding administrative workflows (and any types registered via `ExcludeFromMaxActiveJobs<T>()`).

The count happens once at the start of each dispatch cycle, not per-entry. If you have `MaxActiveJobs = 100` and 95 are active, the dispatcher will take up to 5 entries from the queue. The remaining entries stay `Queued` and get picked up on the next polling cycle.

Setting `MaxActiveJobs` to `null` disables the global check entirely—all queued entries are dispatched (subject to per-group limits).

```csharp
.AddScheduler(scheduler => scheduler
    .MaxActiveJobs(100)                              // limit to 100 concurrent jobs
    .ExcludeFromMaxActiveJobs<IMyInternalWorkflow>() // don't count these
)
```

*API Reference: [AddScheduler — MaxActiveJobs]({{ site.baseurl }}{% link api-reference/scheduler-api/add-scheduler.md %})*

### Per-Group MaxActiveJobs

Each `ManifestGroup` can have its own `MaxActiveJobs` limit, configured from the dashboard on the ManifestGroup detail page. A group's active count only includes jobs belonging to that group—limits are completely independent across groups.

Both limits are enforced simultaneously. In practice, a group can dispatch at most `min(group limit, remaining global capacity)` jobs in a given cycle. When a group hits its per-group cap, the dispatcher uses `continue` to skip that group's entries and keeps processing entries from other groups. This prevents a single busy group from starving lower-traffic groups that still have capacity.

## Why a Separate Workflow

Before the JobDispatcher existed, the ManifestManager handled dispatch directly. `MaxActiveJobs` was checked in its `EnqueueJobsStep`. That worked fine when the ManifestManager was the only source of job execution.

But other sources exist: `TriggerAsync` for manual triggers, the dashboard for re-runs. Each of those had to independently create Metadata and enqueue to Hangfire, bypassing the ManifestManager's capacity check entirely.

The work queue + JobDispatcher pattern fixes this. All sources write to the same queue. The JobDispatcher is the only thing that reads from it. Capacity enforcement happens exactly once, in one place.
