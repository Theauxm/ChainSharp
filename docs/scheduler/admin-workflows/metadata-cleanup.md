---
layout: default
title: MetadataCleanup
parent: Administrative Workflows
grand_parent: Scheduling
nav_order: 4
---

# MetadataCleanupWorkflow

The MetadataCleanup workflow deletes old metadata rows for high-frequency internal workflows. Without it, workflows like ManifestManager (which runs every 5 seconds by default) would generate hundreds of thousands of metadata rows per day.

## Chain

```
DeleteExpiredMetadata
```

One step. It's a simple workflow because the logic is straightforward—the complexity is in the deletion query, not in orchestration.

## How It Runs

The `MetadataCleanupPollingService` is a separate `BackgroundService` from the manifest polling service. It runs on its own interval (`CleanupInterval`, default: 1 minute) and invokes the MetadataCleanupWorkflow each cycle. It also runs a cleanup immediately on startup.

## The Deletion Step

`DeleteExpiredMetadataStep` uses EF Core's `ExecuteDeleteAsync` for efficient bulk deletion—no entities are loaded into memory. It deletes in two passes to respect foreign key constraints:

1. **Delete log entries** for matching metadata rows
2. **Delete metadata rows** themselves

A metadata row is deleted when all three conditions are true:

1. Its `Name` matches a workflow in the `WorkflowTypeWhitelist`
2. Its `StartTime` is older than `RetentionPeriod`
3. Its `WorkflowState` is `Completed` or `Failed`

`Pending` and `InProgress` metadata is never deleted, regardless of age. Only terminal states are eligible.

## Configuration

Enable cleanup with `.AddMetadataCleanup()`:

```csharp
.AddScheduler(scheduler => scheduler
    .AddMetadataCleanup()
)
```

With no arguments, this cleans up `ManifestManagerWorkflow`, `JobDispatcherWorkflow`, and `MetadataCleanupWorkflow` metadata older than 1 hour, checking every minute.

### Custom Configuration

```csharp
.AddMetadataCleanup(cleanup =>
{
    cleanup.RetentionPeriod = TimeSpan.FromHours(2);
    cleanup.CleanupInterval = TimeSpan.FromMinutes(5);
    cleanup.AddWorkflowType<IMyNoisyWorkflow>();
    cleanup.AddWorkflowType("LegacyWorkflowName");
})
```

`AddWorkflowType<T>()` uses `typeof(T).Name` to match the `Name` column in the metadata table. You can also pass a raw string for workflows that aren't easily referenced by type.

### Options

| Option | Default | Description |
|--------|---------|-------------|
| `CleanupInterval` | 1 minute | How often the cleanup service runs |
| `RetentionPeriod` | 1 hour | Age threshold for deletion eligibility |
| `WorkflowTypeWhitelist` | ManifestManager, JobDispatcher, MetadataCleanup | Workflow names whose metadata can be deleted |
