---
layout: default
title: Data Layer
parent: Architecture
nav_order: 2
---

# Data Layer

## DataContext<TDbContext>

```csharp
public class DataContext<TDbContext> : DbContext, IDataContext
    where TDbContext : DbContext
{
    public DbSet<Metadata> Metadatas { get; set; }
    public DbSet<Log> Logs { get; set; }

    // IEffectProvider implementation
    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await base.SaveChangesAsync(cancellationToken);
    }

    public async Task Track(IModel model)
    {
        Add(model);
    }

    // Transaction support
    public async Task<IDataContextTransaction> BeginTransaction() { }
    public async Task CommitTransaction() { }
    public async Task RollbackTransaction() { }
}
```

## Data Model Structure

```
┌──────────────────────────────────────────────────────────────────┐
│                           MANIFEST                                │
├──────────────────────────────────────────────────────────────────┤
│ Id (PK)              │ int                                       │
│ ExternalId           │ string                                    │
│ Name                 │ string                                    │
│ PropertyTypeName     │ string?                                   │
│ Properties           │ jsonb                                     │
│ IsEnabled            │ bool                                      │
│ ScheduleType         │ enum (None/Cron/Interval/Dependent)       │
│ CronExpression       │ string?                                   │
│ IntervalSeconds      │ int?                                      │
│ MaxRetries           │ int                                       │
│ TimeoutSeconds       │ int?                                      │
│ LastSuccessfulRun    │ datetime?                                 │
│ GroupId              │ string?                                   │
│ DependsOnManifestId  │ int? → Self                               │
└──────────────────────────────────────────────────────────────────┘
       │              │                  │
       │ 1:N          │ 1:N              │ 1:N
       ▼              ▼                  ▼
┌────────────┐  ┌────────────────┐  ┌─────────────────────────────┐
│ DEAD_LETTER│  │  WORK_QUEUE    │  │          METADATA            │
├────────────┤  ├────────────────┤  ├─────────────────────────────┤
│ Id     │int│  │ Id       │ int │  │ Id (PK)         │ int       │
│Manifest│int│  │ External │ str │  │ ParentId (FK)   │ int? →Self│
│  Id    │   │  │ Workflow │ str │  │ ManifestId (FK) │ int?      │
│DeadLet │ dt│  │   Name   │     │  │ ExternalId      │ string    │
│ teredAt│   │  │ Input    │json?│  │ Name            │ string    │
│ Status │enm│  │ InputTyp │str? │  │ Executor        │ string?   │
│ Reason │str│  │   eName  │     │  │ WorkflowState   │ enum      │
│RetryC. │int│  │ Status   │enum │  │ FailureStep     │ string?   │
│Resolved│dt?│  │ CreatedAt│ dt  │  │ FailureException│ string?   │
│Resoluti│   │  │Dispatched│ dt? │  │ FailureReason   │ string?   │
│  onNote│str│  │   At     │     │  │ StackTrace      │ string?   │
│RetryMet│   │  │ManifestId│int? │  │ Input           │ jsonb     │
│ adataId│int│  │MetadataId│int? │  │ Output          │ jsonb     │
└────────────┘  └────────────────┘  │ StartTime       │ datetime  │
                                    │ EndTime         │ datetime? │
  DeadLetterStatus:                 │ ScheduledTime   │ datetime? │
  - AwaitingIntervention            └─────────────────────────────┘
  - Retried                                  │
  - Acknowledged                             │ 1:N
                                             ▼
  WorkQueueStatus:              ┌─────────────────────────────────┐
  - Queued                      │              LOG                 │
  - Dispatched                  ├─────────────────────────────────┤
                                │ Id (PK)          │ int          │
                                │ MetadataId (FK)  │ int          │
                                │ EventId          │ int          │
                                │ Level            │ enum         │
                                │ Message          │ string       │
                                │ Category         │ string       │
                                │ Exception        │ string?      │
                                │ StackTrace       │ string?      │
                                └─────────────────────────────────┘
```

The **WorkQueue** table sits between scheduling and dispatch. When a manifest is due (or `TriggerAsync` is called), a `Queued` entry is created. The JobDispatcher reads these, creates a Metadata record, enqueues to the background task server, and flips the status to `Dispatched`. Both the Manifest and Metadata FKs use `ON DELETE RESTRICT`.

Additionally, **StepMetadata** tracks individual step executions within a workflow (not persisted to the database, used in-memory during workflow execution):

```
┌──────────────────────────────────────────────────────────────────┐
│                        STEP_METADATA                              │
├──────────────────────────────────────────────────────────────────┤
│ Id (PK)              │ int                                       │
│ WorkflowName         │ string                                    │
│ WorkflowExternalId   │ string                                    │
│ Name                 │ string                                    │
│ ExternalId           │ string                                    │
│ InputType            │ Type                                      │
│ OutputType           │ Type                                      │
│ State                │ EitherStatus                              │
│ HasRan               │ bool                                      │
│ StartTimeUtc         │ datetime?                                 │
│ EndTimeUtc           │ datetime?                                 │
│ OutputJson           │ string?                                   │
└──────────────────────────────────────────────────────────────────┘
```

## Implementation Variants

### PostgreSQL Implementation

```csharp
public class PostgresContext : DataContext<PostgresContext>
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .HasDefaultSchema("chain_sharp")
            .AddPostgresEnums()                    // PostgreSQL enum types
            .ApplyUtcDateTimeConverter();          // UTC date handling
    }
}
```

**Features:**
- ACID transactions via PostgreSQL
- JSON column support for input/output parameters
- Automatic schema migration
- PostgreSQL-specific optimizations (enums, JSON queries)

### InMemory Implementation

```csharp
public class InMemoryContext : DataContext<InMemoryContext>
{
    // Minimal implementation - inherits all functionality
    // Uses Entity Framework Core's in-memory provider
}
```

**Features:**
- Fast, lightweight for testing
- No external dependencies
- Automatic cleanup between tests
- API-compatible with production database
