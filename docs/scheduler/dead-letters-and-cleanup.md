---
layout: default
title: Dead Letters & Cleanup
parent: Scheduling
nav_order: 3
---

# Dead Letters, Monitoring & Cleanup

## Handling Dead Letters

When a job exceeds `MaxRetries`, it enters the dead letter queue with status `AwaitingIntervention`. The ManifestManager will skip these manifests until they're resolved.

To resolve a dead letter:

```csharp
// Option 1: Retry (creates a new execution)
deadLetter.Status = DeadLetterStatus.Retried;
deadLetter.ResolvedAt = DateTime.UtcNow;
deadLetter.ResolutionNote = "Root cause fixed, retrying";
// Then create a new Metadata and enqueue it

// Option 2: Acknowledge (mark as handled, no retry)
deadLetter.Status = DeadLetterStatus.Acknowledged;
deadLetter.ResolvedAt = DateTime.UtcNow;
deadLetter.ResolutionNote = "Data was manually corrected";

await context.SaveChanges(ct);
```

## Monitoring

The Hangfire Dashboard at `/hangfire` shows enqueued TaskServerExecutor jobs, failures, and worker health. The ManifestManager polling itself runs as a .NET `BackgroundService` outside of Hangfire, so it won't appear in the dashboard. Configure authorization for production.

For workflow-level details, query the `Metadata` table:

```csharp
// Recent failures for a manifest
var failures = await context.Metadatas
    .Where(m => m.ManifestId == manifestId && m.WorkflowState == WorkflowState.Failed)
    .OrderByDescending(m => m.StartTime)
    .Take(10)
    .ToListAsync();
```

## Metadata Cleanup

System workflows like `ManifestManagerWorkflow` run frequently (every 5 seconds by default), generating metadata rows that have no long-term value. The metadata cleanup service automatically purges old entries to keep the database clean.

### How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│        MetadataCleanupPollingService (BackgroundService)         │
│            Polls on CleanupInterval (default: 1 minute)         │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MetadataCleanupWorkflow                         │
│                                                                  │
│  DeleteExpiredMetadataStep:                                      │
│    1. Find metadata matching whitelist + older than retention    │
│    2. Only terminal states (Completed / Failed)                  │
│    3. Delete associated log entries (FK safety)                  │
│    4. Delete metadata rows                                       │
└─────────────────────────────────────────────────────────────────┘
```

The cleanup only targets metadata in **terminal states** (Completed or Failed). Pending and InProgress metadata is never deleted, regardless of age. Associated log entries are deleted first to avoid foreign key constraint violations.

Deletion uses EF Core's `ExecuteDeleteAsync` for efficient single-statement SQL—no entities are loaded into memory.

### Enabling Cleanup

Add `.AddMetadataCleanup()` to your scheduler configuration. By default this cleans up `ManifestManagerWorkflow`, `JobDispatcherWorkflow`, and `MetadataCleanupWorkflow` metadata older than 1 hour, checking every minute.

*API Reference: [AddMetadataCleanup]({{ site.baseurl }}{% link api-reference/scheduler-api/add-metadata-cleanup.md %}) — all configuration options including `RetentionPeriod`, `CleanupInterval`, and adding custom workflow types to the whitelist.*

See [MetadataCleanup](admin-workflows/metadata-cleanup.md) for details on how the cleanup workflow operates internally.

### What Gets Deleted

A metadata row is deleted when **all** of these conditions are true:

1. Its `Name` matches a workflow in the whitelist
2. Its `StartTime` is older than the retention period
3. Its `WorkflowState` is `Completed` or `Failed`

Any log entries associated with deleted metadata are also removed.

## Testing

For integration tests, use the in-memory task server instead of Hangfire:

```csharp
services.AddChainSharpEffects(options => options
    .AddScheduler(scheduler => scheduler.UseInMemoryTaskServer())
);
```

Jobs execute inline, so tests are fast and don't need Hangfire infrastructure.
