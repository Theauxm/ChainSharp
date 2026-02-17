---
layout: default
title: JobDispatcher
parent: Administrative Workflows
grand_parent: Scheduling
nav_order: 2
---

# JobDispatcherWorkflow

The JobDispatcher is the single gateway between the work queue and the background task server. It reads `Queued` entries, enforces `MaxActiveJobs`, creates Metadata records, and enqueues to Hangfire (or whatever `IBackgroundTaskServer` implementation is configured).

## Chain

```
LoadQueuedJobs → DispatchJobs
```

## Steps

### LoadQueuedJobsStep

Loads all `WorkQueue` entries with `Status = Queued`, ordered by `CreatedAt` ascending. FIFO—oldest entries get dispatched first.

### DispatchJobsStep

The core of the dispatcher. For each queued entry, it:

1. **Checks capacity** (if `MaxActiveJobs` is configured): counts all non-excluded `Pending` or `InProgress` metadata in the database. If the count meets or exceeds the limit, the entire cycle is skipped—no entries are dispatched. If there's partial capacity, only that many entries are taken from the queue.

2. **Deserializes the input**: uses `InputTypeName` to resolve the CLR type, then deserializes `Input` from JSON. Type resolution searches all loaded assemblies.

3. **Creates a Metadata record**: a new `Metadata` row with `WorkflowState = Pending`, linked to the manifest (if present). Saved immediately so it gets a database-generated ID.

4. **Updates the work queue entry**: sets `Status = Dispatched`, records the `MetadataId` and `DispatchedAt` timestamp.

5. **Enqueues to the background task server**: calls `IBackgroundTaskServer.EnqueueAsync` with the metadata ID and deserialized input. This is what hands the job to Hangfire.

If any individual entry fails during dispatch (type resolution, serialization, database error), the error is logged and the loop continues to the next entry. One bad entry doesn't block the rest of the queue.

## MaxActiveJobs Enforcement

This is where `MaxActiveJobs` lives. The count is based on `Metadata` rows in `Pending` or `InProgress` state, excluding administrative workflows (and any types registered via `ExcludeFromMaxActiveJobs<T>()`).

The count happens once at the start of each dispatch cycle, not per-entry. If you have `MaxActiveJobs = 100` and 95 are active, the dispatcher will take up to 5 entries from the queue. The remaining entries stay `Queued` and get picked up on the next polling cycle.

Setting `MaxActiveJobs` to `null` disables the check entirely—all queued entries are dispatched.

```csharp
.AddScheduler(scheduler => scheduler
    .MaxActiveJobs(100)                              // limit to 100 concurrent jobs
    .ExcludeFromMaxActiveJobs<IMyInternalWorkflow>() // don't count these
)
```

## Why a Separate Workflow

Before the JobDispatcher existed, the ManifestManager handled dispatch directly. `MaxActiveJobs` was checked in its `EnqueueJobsStep`. That worked fine when the ManifestManager was the only source of job execution.

But other sources exist: `TriggerAsync` for manual triggers, the dashboard for re-runs. Each of those had to independently create Metadata and enqueue to Hangfire, bypassing the ManifestManager's capacity check entirely.

The work queue + JobDispatcher pattern fixes this. All sources write to the same queue. The JobDispatcher is the only thing that reads from it. Capacity enforcement happens exactly once, in one place.
