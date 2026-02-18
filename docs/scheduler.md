---
layout: default
title: Scheduling
nav_order: 6
has_children: true
---

# Scheduling

ChainSharp.Effect.Orchestration.Scheduler adds background job orchestration to workflows. Define a manifest (what to run, when, and how many retries), and the scheduler handles execution, retries, and dead-lettering.

This isn't a traditional cron scheduler. It supports cron expressions, but its design goal is controlled bulk job orchestration—database replication with thousands of table slices, for example—where you need visibility into every execution attempt.

## When to Use the Scheduler

A hosted service with a timer works fine for simple recurring tasks. The Scheduler is for when you need the audit trail: every execution recorded with inputs, outputs, timing, and failure details. Failed jobs retry automatically. Jobs that fail too many times go to a dead letter queue for manual review.

## Core Concepts

### Manifest = Job Definition

A `Manifest` describes a type of job: which workflow it triggers, scheduling rules, retry policies, and default configuration. The `IManifestScheduler` handles the boilerplate—no need to worry about assembly-qualified names or JSON serialization:

```csharp
await scheduler.ScheduleAsync<ISyncCustomersWorkflow, SyncCustomersInput>(
    "sync-customers-us-east",
    new SyncCustomersInput { Region = "us-east", BatchSize = 500 },
    Every.Hours(6),
    opts => opts.MaxRetries = 3);

// For bulk scheduling from a collection, use ScheduleMany:
scheduler.ScheduleMany<ISyncTableWorkflow, SyncTableInput, string>(
    tables,
    table => ($"sync-{table}", new SyncTableInput { TableName = table }),
    Every.Minutes(5),
    prunePrefix: "sync-");
```

*API Reference: [ScheduleAsync]({% link api-reference/scheduler-api/schedule.md %}), [ScheduleMany]({% link api-reference/scheduler-api/schedule-many.md %})*

The scheduler creates the manifest, resolves the correct type names, and serializes the input automatically. Every call is an upsert—safe to run on every startup without duplicating jobs.

### Metadata = Execution Record

Each time a manifest runs, it creates a new `Metadata` record. These are **immutable**—retries create new rows, never mutate existing ones. This gives you a complete audit trail:

```
Manifest: "sync-customers-us-east"
├── Metadata #1: Completed at 10:00:00
├── Metadata #2: Failed at 10:05:00 (timeout)
├── Metadata #3: Failed at 10:10:00 (timeout)
├── Metadata #4: Failed at 10:15:00 (timeout) → Dead-lettered
```

### Dead Letter = Failed Beyond Retry

When a job fails more times than `MaxRetries`, it moves to the dead letter queue. Dead letters require manual intervention—the scheduler won't automatically retry them.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Dead Letter                               │
├─────────────────────────────────────────────────────────────────┤
│ Status: AwaitingIntervention                                    │
│ Reason: Max retries exceeded (3 failures >= 3 max retries)      │
│ DeadLetteredAt: 2026-02-10 10:15:00                            │
└─────────────────────────────────────────────────────────────────┘
```

Operators can retry (which creates a new execution) or acknowledge (mark as handled without retry).

### Dependent Manifests

A manifest can depend on another manifest. Instead of running on a timer, it fires when its parent's `LastSuccessfulRun` advances past the dependent's own. This is how you build ETL chains, post-processing steps, or any workflow that should only run after another succeeds. See [Dependent Workflows](scheduler/dependent-workflows.md).

```csharp
scheduler
    .Schedule<IExtractWorkflow, ExtractInput>(
        "extract", new ExtractInput(), Every.Hours(1))
    .Then<ILoadWorkflow, LoadInput>(
        "load", new LoadInput());
```

*API Reference: [Schedule]({% link api-reference/scheduler-api/schedule.md %}), [Then]({% link api-reference/scheduler-api/dependent-scheduling.md %})*

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│              ManifestPollingService (BackgroundService)           │
│     Runs ManifestManager then JobDispatcher each cycle           │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                      ┌───────────┴───────────┐
                      ▼                       ▼
┌──────────────────────────────┐  ┌──────────────────────────────┐
│   ManifestManagerWorkflow    │  │    JobDispatcherWorkflow      │
│                              │  │                               │
│  LoadManifests               │  │  LoadQueuedJobs               │
│  → ReapFailedJobs            │  │  → DispatchJobs               │
│  → DetermineJobsToQueue      │  │    (enforces MaxActiveJobs)   │
│  → CreateWorkQueueEntries    │  │                               │
└──────────────┬───────────────┘  └──────────────┬────────────────┘
               │                                  │
               │ writes to                        │ reads from
               ▼                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                        WORK_QUEUE                                │
│            Decouples scheduling from dispatch                    │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ dispatched to
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  TaskServerExecutorWorkflow                       │
│                  (runs on Hangfire workers)                       │
│                                                                  │
│  LoadMetadata → ValidateState → ExecuteWorkflow →                │
│                                      UpdateManifest              │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Your Workflow                                 │
│              (Resolved via WorkflowBus)                           │
└─────────────────────────────────────────────────────────────────┘
```

The **ManifestPollingService** is a .NET `BackgroundService` that runs two workflows each cycle: the ManifestManager first, then the JobDispatcher. It supports sub-minute polling (e.g., every 5 seconds). On startup, it seeds any manifests configured via `.Schedule()`, `.ScheduleMany()`, `.Then()`, or `.ThenMany()`.

The **ManifestManagerWorkflow** loads enabled manifests, dead-letters any that have exceeded their retry limit, determines which are due for execution (including [dependent manifests](scheduler/dependent-workflows.md) whose parent has a newer `LastSuccessfulRun`), and writes them to the work queue. It doesn't enqueue anything directly—it just records intent.

The **JobDispatcherWorkflow** reads from the work queue, enforces the `MaxActiveJobs` limit, creates `Metadata` records, and enqueues to the background task server (Hangfire). This is the single gateway to execution. Everything goes through the work queue first—manifest schedules, `TriggerAsync` calls, dashboard re-runs—so capacity enforcement happens in one place.

The **TaskServerExecutorWorkflow** runs on Hangfire workers for each enqueued job. It loads the Metadata and Manifest, validates the job is still pending, executes the target workflow via `IWorkflowBus`, and updates `LastSuccessfulRun` on success.

See [Administrative Workflows](scheduler/admin-workflows.md) for detailed documentation on each internal workflow.

## API Reference

For complete method signatures, all parameters, and detailed usage examples for every scheduling function, see the [Scheduler API Reference]({% link api-reference/scheduler-api.md %}):

- [Schedule / ScheduleAsync]({% link api-reference/scheduler-api/schedule.md %}) — single recurring workflow
- [ScheduleMany / ScheduleManyAsync]({% link api-reference/scheduler-api/schedule-many.md %}) — batch scheduling with pruning
- [Dependent Scheduling]({% link api-reference/scheduler-api/dependent-scheduling.md %}) — Then, ThenMany, ScheduleDependentAsync
- [Manifest Management]({% link api-reference/scheduler-api/manifest-management.md %}) — DisableAsync, EnableAsync, TriggerAsync
- [Scheduling Helpers]({% link api-reference/scheduler-api/scheduling-helpers.md %}) — Every, Cron, Schedule record, ManifestOptions

## Sample Project

A working example with Hangfire, bulk scheduling, metadata cleanup, and the dashboard is in [`samples/ChainSharp.Samples.Scheduler.Hangfire`](https://github.com/Theauxm/ChainSharp/tree/main/samples/ChainSharp.Samples.Scheduler.Hangfire).
