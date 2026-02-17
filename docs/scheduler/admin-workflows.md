---
layout: default
title: Administrative Workflows
parent: Scheduling
nav_order: 5
has_children: true
---

# Administrative Workflows

The scheduler runs four internal workflows to manage the job lifecycle. They're registered automatically when you call `AddScheduler`—you never instantiate them yourself. They're excluded from `MaxActiveJobs` counts and filtered out of dashboard statistics by default.

```
AdminWorkflows.Types:
  - ManifestManagerWorkflow
  - JobDispatcherWorkflow
  - TaskServerExecutorWorkflow
  - MetadataCleanupWorkflow
```

## The Polling Loop

The `ManifestPollingService` is the entry point. It's a .NET `BackgroundService` that runs on the configured `PollingInterval` (default: 5 seconds). Each cycle executes two workflows back-to-back:

1. **ManifestManager**: evaluates which manifests are due, writes to the work queue
2. **JobDispatcher**: reads from the work queue, enforces capacity, dispatches to the background task server

They run sequentially within a single polling cycle. This matters—ManifestManager writes work queue entries that JobDispatcher reads in the same tick. No waiting for the next cycle.

On startup, the polling service also seeds any manifests configured via `.Schedule()`, `.ScheduleMany()`, `.Then()`, or `.ThenMany()`. Seeding failures prevent the host from starting, which is intentional—if your manifest configuration is broken, you want to know immediately, not after the first polling cycle silently does nothing.

## The Work Queue

All job execution flows through the `work_queue` table. This is the key design decision: nothing goes directly to the background task server anymore. The ManifestManager doesn't enqueue jobs. `TriggerAsync` doesn't enqueue jobs. Dashboard re-runs don't enqueue jobs. They all write a `WorkQueue` entry with status `Queued`, and the JobDispatcher picks them up.

This gives you a single enforcement point for `MaxActiveJobs`. Before the work queue existed, capacity limits had to be checked in every code path that could trigger a job. Now there's one gateway, and it's the JobDispatcher.

```
┌──────────────────────┐
│  ManifestManager     │──┐
│  (scheduled jobs)    │  │
└──────────────────────┘  │
                          │    ┌─────────────┐    ┌───────────────┐    ┌──────────┐
┌──────────────────────┐  ├──► │  WORK_QUEUE │──► │ JobDispatcher │──► │ Hangfire │
│  TriggerAsync        │──┤    └─────────────┘    └───────────────┘    └──────────┘
│  (manual trigger)    │  │
└──────────────────────┘  │
                          │
┌──────────────────────┐  │
│  Dashboard           │──┘
│  (re-runs)           │
└──────────────────────┘
```

A `WorkQueue` entry tracks:
- **WorkflowName**: which workflow to run (fully qualified type name)
- **Input / InputTypeName**: serialized input and its type, for deserialization at dispatch time
- **Status**: `Queued` → `Dispatched`
- **ManifestId**: optional link back to the originating manifest
- **MetadataId**: set by the JobDispatcher when it creates the execution record

## Excluding Workflows from MaxActiveJobs

Administrative workflows are excluded from the active job count by default. If you have your own high-frequency internal workflows that shouldn't count against the limit, exclude them in the builder:

```csharp
.AddScheduler(scheduler => scheduler
    .MaxActiveJobs(100)
    .ExcludeFromMaxActiveJobs<IMyInternalWorkflow>()
)
```
