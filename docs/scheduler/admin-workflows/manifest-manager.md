---
layout: default
title: ManifestManager
parent: Administrative Workflows
grand_parent: Scheduling
nav_order: 1
---

# ManifestManagerWorkflow

The ManifestManager is the first half of each polling cycle. It figures out which manifests are due for execution and writes them to the work queue. It doesn't dispatch anything—that's the [JobDispatcher's](job-dispatcher.md) job.

## Chain

```
LoadManifests → ReapFailedJobs → DetermineJobsToQueue → CreateWorkQueueEntries
```

## Steps

### LoadManifestsStep

Loads all enabled manifests from the database in a single query, eagerly including their ManifestGroup, metadata, dead letters, and work queue entries. Uses `AsSplitQuery()` to avoid cartesian explosion from multiple `Include` calls.

The full object graph is loaded once and passed through the chain. Later steps don't hit the database again to check manifest state—they use the navigations already in memory.

### ReapFailedJobsStep

Scans loaded manifests for any whose failure count meets or exceeds `MaxRetries`. For each, it creates a `DeadLetter` record with status `AwaitingIntervention` and persists immediately.

A manifest is only reaped if it doesn't already have an unresolved dead letter. This prevents duplicate dead letters from accumulating when the same manifest fails across multiple polling cycles.

The step returns the list of newly created dead letters so `DetermineJobsToQueueStep` can skip those manifests without re-querying the database.

### DetermineJobsToQueueStep

The decision step. It runs two passes over the loaded manifests:

**Pass 1: Time-based manifests** (Cron and Interval). For each, it checks whether the manifest is due using `SchedulingHelpers.ShouldRunNow()`, which dispatches to either cron parsing or interval arithmetic based on the schedule type.

**Pass 2: Dependent manifests**. For each manifest with `ScheduleType.Dependent`, it finds the parent in the loaded set and checks whether `parent.LastSuccessfulRun > dependent.LastSuccessfulRun`. See [Dependent Workflows](../dependent-workflows.md).

Manifests with `ScheduleType.DormantDependent` are excluded from **both** passes. They are never auto-queued by the ManifestManager—dormant dependents must be explicitly activated at runtime by the parent workflow via [`IDormantDependentContext`](../dependent-workflows.md#dormant-dependents).

Both passes apply the same per-manifest guards before evaluating the schedule:
- Skip if the manifest's ManifestGroup has `IsEnabled = false`
- Skip if the manifest was just dead-lettered this cycle
- Skip if it has an `AwaitingIntervention` dead letter
- Skip if it has a `Queued` work queue entry (already waiting to be dispatched)
- Skip if it has `Pending` or `InProgress` metadata (already running)

`MaxActiveJobs` is deliberately **not** enforced here. The ManifestManager freely identifies all due manifests. The JobDispatcher handles capacity gating at dispatch time. This keeps the two concerns separate—scheduling logic doesn't need to know about system-wide capacity.

### CreateWorkQueueEntriesStep

For each manifest identified as due, creates a `WorkQueue` entry with:
- `WorkflowName` from the manifest's `Name`
- `Input` / `InputTypeName` from the manifest's `Properties` / `PropertyTypeName`
- `ManifestId` linking back to the source manifest
- `Priority` set from `ManifestGroup.Priority` (the group's priority, not an individual manifest priority)
- `Status = Queued`

For dependent manifests, `DependentPriorityBoost` is still added on top of the group priority at dispatch time.

Each entry is saved individually. If one fails (e.g., a serialization issue for a specific manifest), the others still get queued. Errors are logged per-manifest.

## What Changed

Previously, this workflow had an `EnqueueJobsStep` as its final step. That step would directly create Metadata records and enqueue to the background task server (Hangfire). `MaxActiveJobs` was enforced there, meaning the ManifestManager was both the scheduler and the dispatcher.

Now those responsibilities are split. The ManifestManager writes intent to the work queue. The [JobDispatcher](job-dispatcher.md) reads from it and handles the actual dispatch. This means `TriggerAsync`, dashboard re-runs, and scheduled manifests all converge on the same dispatch path.
