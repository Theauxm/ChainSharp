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
│ Id (PK)            │ int                                         │
│ ExternalId         │ string                                      │
│ Name               │ string                                      │
│ PropertyTypeName   │ string?                                     │
│ Properties         │ jsonb                                       │
│ IsEnabled          │ bool                                        │
│ ScheduleType       │ enum (None/Cron/Interval/OnDemand)          │
│ CronExpression     │ string?                                     │
│ IntervalSeconds    │ int?                                        │
│ MaxRetries         │ int                                         │
│ TimeoutSeconds     │ int?                                        │
│ LastSuccessfulRun  │ datetime?                                   │
│ GroupId            │ string?                                     │
└──────────────────────────────────────────────────────────────────┘
          │                              │
          │ 1:N                          │ 1:N
          ▼                              ▼
┌─────────────────────────────┐  ┌─────────────────────────────────┐
│        DEAD_LETTER          │  │           METADATA               │
├─────────────────────────────┤  ├─────────────────────────────────┤
│ Id (PK)          │ int      │  │ Id (PK)            │ int        │
│ ManifestId (FK)  │ int      │  │ ParentId (FK)      │ int?  → Self │
│ DeadLetteredAt   │ datetime │  │ ManifestId (FK)    │ int?       │
│ Status           │ enum     │  │ ExternalId         │ string     │
│ Reason           │ string   │  │ Name               │ string     │
│ RetryCount       │ int      │  │ Executor           │ string?    │
│ ResolvedAt       │ datetime?│  │ WorkflowState      │ enum       │
│ ResolutionNote   │ string?  │  │ FailureStep        │ string?    │
│ RetryMetadataId  │ int?     │  │ FailureException   │ string?    │
└─────────────────────────────┘  │ FailureReason      │ string?    │
                                 │ StackTrace         │ string?    │
  DeadLetterStatus:              │ Input              │ jsonb      │
  - AwaitingIntervention         │ Output             │ jsonb      │
  - Retried                      │ StartTime          │ datetime   │
  - Acknowledged                 │ EndTime            │ datetime?  │
                                 │ ScheduledTime      │ datetime?  │
                                 └─────────────────────────────────┘
                                          │
                                          │ 1:N
                                          ▼
                                 ┌─────────────────────────────────┐
                                 │              LOG                 │
                                 ├─────────────────────────────────┤
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
