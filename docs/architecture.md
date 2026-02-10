---
layout: default
title: Architecture
nav_order: 5
---

# Architecture

How ChainSharp's components fit together.

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Client Layer                                │
│    [CLI Applications]  [Web Applications]  [API Controllers]        │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  ChainSharp.Effect.Scheduler (Optional)             │
│    [ManifestManager] ───► [ManifestExecutor] ───► [DeadLetter]     │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  ChainSharp.Effect.Mediator                         │
│         [WorkflowBus] ────────► [WorkflowRegistry]                  │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     ChainSharp.Effect                               │
│    [EffectWorkflow] ────► [EffectRunner] ────► [EffectProviders]   │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Core ChainSharp                                │
│           [Workflow Engine] ────────► [Steps]                       │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Effect Implementations                           │
│  [Data Provider]  [JSON Provider]  [Parameter Provider]  [Custom]   │
└────────────┬──────────────────────────────────────────┬─────────────┘
             │                                          │
             ▼                                          ▼
┌─────────────────────────┐                ┌─────────────────────────┐
│       PostgreSQL        │                │        InMemory         │
└─────────────────────────┘                └─────────────────────────┘
```

### Package Hierarchy

```
ChainSharp (Core)
    │
    └─── ChainSharp.Effect (Enhanced Workflows)
              │
              ├─── ChainSharp.Effect.Mediator (WorkflowBus)
              │
              ├─── ChainSharp.Effect.Data (Abstract Persistence)
              │         │
              │         ├─── ChainSharp.Effect.Data.Postgres
              │         └─── ChainSharp.Effect.Data.InMemory
              │
              ├─── ChainSharp.Effect.Scheduler (Job Orchestration)
              │         │
              │         └─── ChainSharp.Effect.Scheduler.Hangfire
              │
              ├─── ChainSharp.Effect.Provider.Json
              ├─── ChainSharp.Effect.Provider.Parameter
              └─── ChainSharp.Effect.StepProvider.Logging
```

## Core Component Hierarchy

### 1. ChainSharp (Core Engine)

The foundation layer providing Railway Oriented Programming patterns.

#### Key Classes

```csharp
// Base workflow class
public abstract class Workflow<TIn, TOut>
{
    public Task<TOut> Run(TIn input);
    
    protected Workflow<TIn, TOut> Activate(TIn input);
}

// Step interface for individual operations
public interface IStep<TIn, TOut>
{
    Task<TOut> Run(TIn input);
}

// Chain class for composing steps
public class Chain<T>
{
    public Chain<TOut> Chain<TStep, TOut>() where TStep : IStep<T, TOut>;
    public Task<Either<Exception, T>> Resolve();
}
```

This layer handles chaining, error propagation, and the core workflow lifecycle.

### 2. ChainSharp.Effect (Enhanced Workflows)

Extends core workflows with dependency injection, metadata tracking, and effect management.

#### EffectWorkflow<TIn, TOut>

```csharp
public abstract class EffectWorkflow<TIn, TOut> : Workflow<TIn, TOut>, IEffectWorkflow<TIn, TOut>
{
    // Internal framework properties (injected automatically)
    [Inject] public IEffectRunner? EffectRunner { get; set; }
    [Inject] public ILogger<EffectWorkflow<TIn, TOut>>? EffectLogger { get; set; }
    [Inject] public IServiceProvider? ServiceProvider { get; set; }
    
    public Metadata Metadata { get; private set; }
    protected string WorkflowName => GetType().Name;
    protected int? ParentId { get; set; }
    
    public sealed override async Task<Either<Exception, TOut>> Run(TIn input)
    {
        // 1. Initialize metadata and start tracking
        Metadata = await InitializeWorkflow(input);
        
        try
        {
            // 2. Execute the actual workflow logic
            var result = await RunInternal(input);
            
            // 3. Finalize metadata and save effects
            await FinishWorkflow(result);
            await EffectRunner.SaveChanges(CancellationToken.None);
            
            return result;
        }
        catch (Exception ex)
        {
            // 4. Handle failures and save error state
            await FinishWorkflow(Either<Exception, TOut>.Left(ex));
            await EffectRunner.SaveChanges(CancellationToken.None);
            throw;
        }
    }
    
    protected abstract Task<Either<Exception, TOut>> RunInternal(TIn input);
}
```

#### EffectRunner

```csharp
public class EffectRunner : IEffectRunner
{
    private List<IEffectProvider> ActiveEffectProviders { get; init; }

    public EffectRunner(IEnumerable<IEffectProviderFactory> effectProviderFactories)
    {
        ActiveEffectProviders = [];
        ActiveEffectProviders.AddRange(
            effectProviderFactories.RunAll(factory => factory.Create())
        );
    }

    public async Task Track(IModel model)
    {
        ActiveEffectProviders.RunAll(provider => provider.Track(model));
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await ActiveEffectProviders.RunAllAsync(
            provider => provider.SaveChanges(cancellationToken)
        );
    }

    public void Dispose() => DeactivateProviders();
}
```

This layer adds metadata tracking, effect coordination, and handles the workflow lifecycle (create metadata → run steps → save effects).

### 3. Effect Providers Architecture

Effect providers implement the `IEffectProvider` interface to handle different concerns.

#### IEffectProvider Interface

```csharp
public interface IEffectProvider : IDisposable
{
    Task SaveChanges(CancellationToken cancellationToken);
    Task Track(IModel model);
}
```

#### Available Effect Providers

| Provider | Package | Purpose | Performance Impact |
|----------|---------|---------|-------------------|
| **DataContext** | ChainSharp.Effect.Data | Database persistence | Medium - Database I/O |
| **JsonEffect** | ChainSharp.Effect.Provider.Json | Debug logging | Low - JSON serialization |
| **ParameterEffect** | ChainSharp.Effect.Provider.Parameter | Parameter serialization | Medium - JSON + Storage |
| **Custom Providers** | User-defined | Application-specific | Varies |

## 4. ChainSharp.Effect.Data (Persistence Layer)

### DataContext<TDbContext>

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

### Data Model Structure

```
┌─────────────────────────────────────────────────────────────────┐
│                           METADATA                               │
├─────────────────────────────────────────────────────────────────┤
│ Id (PK)          │ int                                          │
│ ParentId (FK)    │ int?           → Self-reference              │
│ ExternalId       │ string                                       │
│ Name             │ string                                       │
│ Executor         │ string                                       │
│ WorkflowState    │ enum                                         │
│ FailureStep      │ string?                                      │
│ FailureException │ string?                                      │
│ FailureReason    │ string?                                      │
│ StackTrace       │ string?                                      │
│ Input            │ json                                         │
│ Output           │ json                                         │
│ StartTime        │ datetime                                     │
│ EndTime          │ datetime?                                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ 1:N
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                             LOG                                  │
├─────────────────────────────────────────────────────────────────┤
│ Id (PK)          │ int                                          │
│ MetadataId (FK)  │ int            → METADATA.Id                 │
│ LogLevel         │ enum                                         │
│ Message          │ string                                       │
│ Properties       │ json                                         │
│ Timestamp        │ datetime                                     │
└─────────────────────────────────────────────────────────────────┘
```

### Implementation Variants

#### PostgreSQL Implementation

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
- Production-ready with ACID transactions
- JSON column support for input/output parameters
- Automatic schema migration
- PostgreSQL-specific optimizations (enums, JSON queries)

#### InMemory Implementation

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

## 5. ChainSharp.Effect.Mediator (Discovery & Routing)

### WorkflowRegistry

```csharp
public class WorkflowRegistry : IWorkflowRegistry
{
    public Dictionary<Type, Type> InputTypeToWorkflow { get; set; }

    public WorkflowRegistry(params Assembly[] assemblies)
    {
        // Scan assemblies for workflow implementations
        var workflowType = typeof(IEffectWorkflow<,>);
        var allWorkflowTypes = ScanAssembliesForWorkflows(assemblies, workflowType);
        
        // Create mapping: InputType -> WorkflowType
        InputTypeToWorkflow = allWorkflowTypes.ToDictionary(
            workflowType => ExtractInputType(workflowType),
            workflowType => workflowType
        );
    }
}
```

### WorkflowBus

```csharp
public class WorkflowBus : IWorkflowBus
{
    public async Task<TOut> RunAsync<TOut>(object workflowInput, Metadata? parentMetadata = null)
    {
        // 1. Find workflow type by input type
        var inputType = workflowInput.GetType();
        var workflowType = _registryService.InputTypeToWorkflow[inputType];
        
        // 2. Resolve workflow from DI container
        var workflow = _serviceProvider.GetRequiredService(workflowType);
        
        // 3. Inject internal workflow properties (framework-level only)
        _serviceProvider.InjectProperties(workflow);
        
        // 4. Set up parent-child relationship if needed
        if (parentMetadata != null)
            SetParentId(workflow, parentMetadata.Id);
        
        // 5. Execute workflow using reflection
        return await InvokeWorkflowRun<TOut>(workflow, workflowInput);
    }
}
```

### Key Constraints and Design Decisions

#### Input Type Uniqueness Constraint

**Critical:** Each input type can only map to ONE workflow.

```csharp
// ❌ This will cause conflicts
public class CreateUserWorkflow : EffectWorkflow<UserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UserRequest, User> { }

// ✅ This works correctly  
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UpdateUserRequest, User> { }
```

#### Workflow Discovery Rules

1. **Must be concrete classes** (not abstract)
2. **Must implement IEffectWorkflow<,>**
3. **Must have parameterless constructor or be registered in DI**
4. **Should implement a non-generic interface** for better DI integration
