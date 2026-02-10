# ChainSharp.Effect.Scheduler - Design Summary

## Overview

`ChainSharp.Effect.Scheduler` is a job orchestration layer for ChainSharp workflows. It provides:
- **Manifest-based job definitions** - Declarative descriptions of jobs that can be run
- **Controlled execution** - Avoids overwhelming background task servers with thousands of queued jobs
- **Failure tracking** - Full audit trail of every execution attempt
- **Dead letter queue** - Manual intervention workflow for repeatedly failing jobs

This is NOT a traditional cron-based scheduler. While it supports cron expressions, its primary design goal is **controlled bulk job orchestration** (e.g., database replication with thousands of table slices) with full visibility into failures.

---

## Core Philosophy

### Manifest = Job Definition (What CAN run)
A `Manifest` describes a type of job: which workflow it triggers, scheduling rules, retry policies, and default configuration.

### Metadata = Execution Record (What DID run)
A `Metadata` is an **immutable record** of a single execution attempt. Each retry creates a NEW `Metadata` row—we never mutate execution history.

### Why Immutable Executions?
- **Full audit trail**: Every attempt is preserved with its input, output, failure details, and timing
- **Derived retry count**: `SELECT COUNT(*) FROM Metadata WHERE ManifestId = X AND WorkflowState = 'Failed'`
- **No lost state**: If the system crashes, you know exactly what happened
- **Debugging**: Compare inputs/outputs across retries to understand failures

---

## Model Changes

### Manifest ✅ (Implemented)

| Column | Type | Purpose |
|--------|------|---------|
| `is_enabled` | `bool` | Allows pausing/resuming a job without deleting it. The ManifestManager skips disabled manifests. |
| `schedule_type` | `enum` | Determines how/when this manifest should be triggered: `None` (manual only), `Cron`, `Interval`, `OnDemand` (bulk operations). |
| `cron_expression` | `string?` | Standard cron expression for `Cron` schedule type. Null for other types. |
| `interval_seconds` | `int?` | For `Interval` type - simpler than cron for basic "every N seconds" patterns. |
| `max_retries` | `int` | Policy: how many failed executions before dead-lettering? Retries are NEW Metadata rows, not mutations. |
| `timeout_seconds` | `int?` | How long before an "InProgress" execution is considered stuck. Null = use global default. |
| `last_successful_run` | `DateTime?` | Tracks when this manifest last completed successfully. Useful for scheduling decisions and "delta mode" workflows. |

**Existing columns retained:**
- `id`, `external_id`, `name`, `property_type`, `properties` (unchanged)
- `Metadatas` navigation property (1:N relationship)

### Metadata ✅ (Implemented)

| Column | Type | Purpose |
|--------|------|---------|
| `manifest_id` | `int?` | FK to the Manifest (job definition). Added via migration `7_metadata_manifest_fk.sql`. |
| `scheduled_time` | `DateTime?` | When this execution was *supposed* to run (vs `StartTime` which is when it actually started). Useful for SLA tracking and understanding delays. |

**Existing columns retained:**
- Failure tracking: `FailureStep`, `FailureException`, `FailureReason`, `StackTrace`
- Timing: `StartTime`, `EndTime`
- State: `WorkflowState` (Pending → InProgress → Completed/Failed)

### DeadLetter ✅ (Implemented)

| Column | Type | Purpose |
|--------|------|---------|
| `id` | `int` | PK |
| `manifest_id` | `int` | FK to the Manifest (job definition) |
| `dead_lettered_at` | `DateTime` | When the job was moved to dead letter queue |
| `reason` | `string` | Why it was dead-lettered (e.g., "Max retries exceeded") |
| `status` | `enum` | `AwaitingIntervention`, `Retried`, `Acknowledged` |
| `resolved_at` | `DateTime?` | When the dead letter was resolved |
| `resolution_note` | `string?` | Operator notes (e.g., "Data was manually corrected") |
| `retry_count_at_dead_letter` | `int` | How many attempts were made before dead-lettering |
| `retry_metadata_id` | `int?` | FK to new Metadata if retried |

---

## Enums ✅ (Implemented)

Located in `ChainSharp.Effect/Enums/`:

### ScheduleType
```csharp
public enum ScheduleType
{
    None,      // Manual trigger only (via API/code)
    Cron,      // Traditional cron expression
    Interval,  // Every N seconds
    OnDemand   // Designed for bulk operations
}
```

### DeadLetterStatus
```csharp
public enum DeadLetterStatus
{
    AwaitingIntervention,  // Needs manual review
    Retried,               // A new execution was created
    Acknowledged           // Operator marked as handled (no retry)
}
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Your Application                              │
│         [API Controllers]  [Background Jobs]  [CLI]             │
└─────────────────────────────────┬───────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ChainSharp.Effect.Scheduler                     │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │   ManifestManagerWorkflow : EffectWorkflow<Unit, PollResult>│   │
│  │                                                            │   │
│  │  Activate(Unit)                                            │   │
│  │      .Chain<ReapFailedJobsStep>()                          │   │
│  │      .Chain<DetermineJobsToQueueStep>()                    │   │
│  │      .Chain<EnqueueJobsStep>()                             │   │
│  │      .Resolve()                                            │   │
│  └──────────────────────────────┬───────────────────────────┘   │
│                                 │                                │
│                                 ▼                                │
│                    ┌──────────────────────┐                     │
│                    │ IBackgroundTaskServer│ ◄── Abstraction     │
│                    │ (Hangfire/Quartz/etc)│                     │
│                    └──────────┬───────────┘                     │
│                               │                                  │
│                               ▼                                  │
│                    ┌──────────────────┐                         │
│                    │ ManifestExecutor │                         │
│                    │ (runs workflows) │                         │
│                    └────────┬─────────┘                         │
└─────────────────────────────┼───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ChainSharp.Effect.Mediator                      │
│                      [WorkflowBus]                               │
└─────────────────────────────────────────────────────────────────┘
```

---

## ManifestManagerWorkflow

The `ManifestManagerWorkflow` is an `EffectWorkflow<Unit, PollResult>` that runs on a recurring schedule (e.g., once per minute) via IBackgroundTaskServer. It uses the standard ChainSharp step pattern:

```csharp
public class ManifestManagerWorkflow : EffectWorkflow<Unit, PollResult>
{
    protected override async Task<Either<Exception, PollResult>> RunInternal(Unit input)
        => Activate(input)
            .Chain<ReapFailedJobsStep>()
            .Chain<DetermineJobsToQueueStep>()
            .Chain<EnqueueJobsStep>()
            .Resolve();
}
```

### Why `EffectWorkflow`?

Using `EffectWorkflow` provides:
- **Metadata tracking**: Each poll execution is recorded with start time, end time, success/failure status
- **Visibility**: Can query "when did the last poll run?" and "did it fail?"
- **Consistency**: Follows the same pattern as all other workflows in ChainSharp

**Trade-off**: EffectWorkflow calls `SaveChanges()` atomically at the end. If `EnqueueJobsStep` succeeds but something after fails, DeadLetter records from step 1 could be rolled back. Steps that need immediate persistence should call `SaveChanges()` explicitly within the step.

### Step Details

```
ManifestManagerWorkflow.Run(Unit)
    │
    ├─1─► ReapFailedJobsStep : Step<Unit, List<DeadLetter>>
    │     Input: Unit
    │     Output: List<DeadLetter> (newly created dead letters)
    │     
    │     - Query manifests where failed execution count >= max_retries
    │     - Create DeadLetter records with status AwaitingIntervention
    │     - SaveChanges() immediately (dead letters persist regardless of later steps)
    │
    ├─2─► DetermineJobsToQueueStep : Step<Unit, List<Manifest>>
    │     Input: Unit (reads dead letters from Memory if needed)
    │     Output: List<Manifest> (manifests due for execution)
    │     
    │     - Query enabled manifests due for execution based on:
    │       - schedule_type (Cron, Interval, OnDemand)
    │       - cron_expression / interval_seconds
    │       - last_successful_run
    │     - Filter out manifests already in dead letter queue
    │     - Filter out manifests with pending/in-progress executions
    │
    └─3─► EnqueueJobsStep : Step<List<Manifest>, PollResult>
          Input: List<Manifest> (from previous step)
          Output: PollResult (summary of what was enqueued)
          
          For each manifest:
            - Create new Metadata row with state = Pending
            - SaveChanges() immediately
            - Enqueue ManifestExecutor.ExecuteAsync(metadataId) to IBackgroundTaskServer
```

### Why This Order?

1. **Reap first**: Dead-lettering happens before queuing to prevent re-queuing jobs that have already failed too many times
2. **Determine second**: Scheduling logic runs after reaping so it can skip dead-lettered manifests
3. **Enqueue last**: Only after all decisions are made do we actually create Metadata and enqueue work

---

## Component Responsibilities

| Component | Responsibility |
|-----------|---------------|
| **ManifestManagerWorkflow** | Orchestration: chains the three poll steps together, runs on schedule via IBackgroundTaskServer |
| **ReapFailedJobsStep** | Dead-letter logic: finds manifests exceeding max_retries, creates DeadLetter records, persists immediately |
| **DetermineJobsToQueueStep** | Scheduling logic: queries enabled manifests due for execution based on schedule_type, filters out dead-lettered/in-progress |
| **EnqueueJobsStep** | Enqueue logic: creates Pending Metadata rows, enqueues ManifestExecutor calls to IBackgroundTaskServer |
| **ManifestExecutor** | Execution: runs a single workflow from a Pending Metadata row (already implemented ✅) |
| **IBackgroundTaskServer** | Queue abstraction: Hangfire/Quartz/etc. - receives enqueued ManifestExecutor calls, runs ManifestManagerWorkflow on schedule |
| **Scheduling Utilities** | Helper functions for cron parsing, interval checks - used by DetermineJobsToQueueStep |

---

## What Has Been Implemented

### Project Structure ✅
- `ChainSharp.Effect.Scheduler.csproj` - Project file with dependencies
- Added to solution file

### Interfaces & Services
- `IBackgroundTaskServer` - Abstraction over Hangfire/Quartz/etc. (interface only)
- `ManifestManagerWorkflow` - Workflow that orchestrates the poll (not yet implemented)
- `ReapFailedJobsStep` - Step for dead-letter reaping (not yet implemented)
- `DetermineJobsToQueueStep` - Step for scheduling logic (not yet implemented)
- `EnqueueJobsStep` - Step for creating Metadata and enqueuing (not yet implemented)
- `IManifestExecutor` / `ManifestExecutor` ✅ **Implemented** - Workflow execution from scheduled jobs

### Models ✅
- `Manifest` - Job definition with all scheduling columns (`is_enabled`, `schedule_type`, `cron_expression`, `interval_seconds`, `max_retries`, `timeout_seconds`, `last_successful_run`)
- `Metadata` - Execution record with `scheduled_time` and `manifest_id` FK
- `DeadLetter` - Dead letter queue entity (model implemented, service not yet)
- Enums: `ScheduleType`, `DeadLetterStatus` in `ChainSharp.Effect/Enums/`

### Database Migrations ✅
- Migration `7_metadata_manifest_fk.sql` - Adds `manifest_id` FK and `scheduled_time` to Metadata
- DeadLetter table migration (working)
- `DeadLetter` DbSet registered in `IDataContext`

### Configuration ✅
- `SchedulerConfiguration` - Global scheduler settings

### Extensions ✅
- `SchedulerExtensions` - DI registration helpers

### ManifestExecutor ✅ (Implemented)

The `ManifestExecutor` service executes workflow jobs that have been scheduled via the manifest system:

**Implementation details:**
- Loads Metadata by ID with Manifest navigation property
- Validates state (must be `Pending` to execute)
- Resolves input from Manifest properties via `GetProperties()`
- Executes workflow via `IWorkflowBus.RunAsync()`
- Updates `LastSuccessfulRun` on the Manifest after successful execution
- Persists changes via `IDataContext.SaveChanges()`

**Location:** `Services/ManifestExecutor/ManifestExecutor.cs`

### Integration Tests ✅ (Implemented)

Full integration test suite for `ManifestExecutor` located in `tests/ChainSharp.Tests.Effect.Scheduler.Integration/`:

| Test | Description |
|------|-------------|
| `ExecuteAsync_WhenMetadataNotFound_ThrowsInvalidOperationException` | Verifies exception when metadata ID doesn't exist |
| `ExecuteAsync_WhenStateIsCompleted_ThrowsWorkflowException` | Verifies exception when state is Completed |
| `ExecuteAsync_WhenStateIsFailed_ThrowsWorkflowException` | Verifies exception when state is Failed |
| `ExecuteAsync_WhenStateIsInProgress_ThrowsWorkflowException` | Verifies exception when state is InProgress |
| `ExecuteAsync_WhenManifestIsNull_ThrowsInvalidOperationException` | Verifies exception when manifest not loaded |
| `ExecuteAsync_WhenStateIsPending_ExecutesWorkflowSuccessfully` | Verifies workflow bus is called correctly |
| `ExecuteAsync_WhenSuccessful_UpdatesLastSuccessfulRunOnManifest` | Verifies timestamp update |
| `ExecuteAsync_WithDifferentInputValues_ExecutesCorrectly` | Verifies different inputs work |
| `ExecuteAsync_WhenCancellationRequested_ThrowsOperationCanceledException` | Verifies cancellation token propagation |

**Test project features:**
- Uses real Postgres database (no mocks)
- Real DI container with all ChainSharp effects configured
- Real `IWorkflowBus`, `IDataContext`, and `IManifestExecutor`
- Test workflows that implement actual ChainSharp patterns

---

## What Still Needs Implementation

### 1. Workflow & Steps (ChainSharp.Effect.Scheduler)

**ManifestManagerWorkflow:**
- `Workflow<Unit, PollResult>` that chains the three steps
- Called by IBackgroundTaskServer on recurring schedule

**ReapFailedJobsStep : Step<Unit, List<DeadLetter>>**
- Query manifests where failed execution count >= max_retries
- Create DeadLetter records with status AwaitingIntervention
- Call `IDataContext.SaveChanges()` immediately
- Handle stuck job recovery (jobs stuck in InProgress past timeout_seconds)

**DetermineJobsToQueueStep : Step<Unit, List<Manifest>>**
- Query enabled manifests due for execution based on schedule_type, cron/interval, last_successful_run
- Filter out manifests already in dead letter queue (status = AwaitingIntervention)
- Filter out manifests with pending/in-progress executions
- Concurrency controls (prevent duplicate queuing of same manifest)

**EnqueueJobsStep : Step<List<Manifest>, PollResult>**
- For each manifest: create Pending Metadata row, SaveChanges, enqueue to IBackgroundTaskServer
- Return PollResult with summary (jobs enqueued, dead letters created, etc.)

**Scheduling Utilities:**
- Cron expression parsing (use Cronos or similar library)
- Interval calculation helpers
- `ShouldRunNow(manifest)` predicate for DetermineJobsToQueueStep

### 2. Background Task Server Implementation

**Option A: In-repo abstraction only**
- Keep `IBackgroundTaskServer` interface
- Document that consumers must provide implementation

**Option B: Separate Hangfire package**
- Create `ChainSharp.Effect.Scheduler.Hangfire` project
- Implement `HangfireTaskServer : IBackgroundTaskServer`
- Register recurring ManifestManager job

### 3. Testing

- ✅ Integration tests for ManifestExecutor with Postgres (9 tests passing)
- Unit tests for ManifestManager scheduling logic
- Unit tests for ManifestManager dead-letter reaping logic
- Integration tests with InMemory data context

### 4. Documentation

- Update `/docs/` with Scheduler usage guide
- Document IBackgroundTaskServer implementation requirements
- Add examples for bulk job enqueuing

---

## Usage Example (Future)

```csharp
// Program.cs
services.AddChainSharpEffects(options => 
    options.AddPostgresEffect(connectionString));

services.AddChainSharpScheduler(options =>
{
    options.PollingInterval = TimeSpan.FromSeconds(30);
    options.DefaultMaxRetries = 3;
});

// Add your background task server
services.AddHangfireTaskServer(config => 
    config.UseSqlServerStorage(connectionString));
```

```csharp
// Creating a manifest for a replication job
var manifest = Manifest.Create(new CreateManifest
{
    Name = typeof(ReplicateTableWorkflow),
    ScheduleType = ScheduleType.OnDemand,
    MaxRetries = 5,
    TimeoutSeconds = 3600,
    Properties = new ReplicationConfig { Source = "prod", Target = "replica" }
});

await dataContext.Track(manifest);
await dataContext.SaveChanges(ct);

// Bulk enqueue 100 table slices
var inputs = Enumerable.Range(0, 100)
    .Select(i => new ReplicateTableInput { TableName = "users", IdIndex = i });

await manifestManager.BulkEnqueueAsync(manifest.Id, inputs, ct);
```
