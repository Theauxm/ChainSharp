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
```

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

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│              ManifestPollingService (BackgroundService)           │
│          Polls ManifestManager on a configurable interval        │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ManifestManagerWorkflow                         │
│                                                                  │
│  LoadManifests → ReapFailedJobs → DetermineJobsToQueue →        │
│                                        EnqueueJobs               │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ Enqueues jobs to Hangfire
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  TaskServerExecutorWorkflow                        │
│                  (runs on Hangfire workers)                       │
│                                                                  │
│  LoadMetadata → ValidateState → ExecuteWorkflow →               │
│                                      UpdateManifest              │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Your Workflow                                │
│              (Resolved via WorkflowBus)                          │
└─────────────────────────────────────────────────────────────────┘
```

The **ManifestPollingService** is a .NET `BackgroundService` that runs the ManifestManager on a configurable interval. It supports sub-minute polling (e.g., every 5 seconds)—something that wasn't possible when the manager was triggered by a Hangfire cron job. On startup, it also seeds any manifests configured via `.Schedule()` or `.ScheduleMany()`.

The **ManifestManagerWorkflow** loads enabled manifests, dead-letters any that have exceeded their retry limit, determines which are due for execution, and enqueues them to the background task server (Hangfire).

The **TaskServerExecutorWorkflow** runs on Hangfire workers for each enqueued job. It loads the Metadata and Manifest, validates the job is still pending, executes the target workflow via `IWorkflowBus`, and updates `LastSuccessfulRun` on success.
