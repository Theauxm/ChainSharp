---
layout: default
title: Architecture
nav_order: 4
---

# System Architecture

This document provides comprehensive technical information about ChainSharp's components, their relationships, and how they work together to form a cohesive workflow engine.

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Client Layer                                │
│    [CLI Applications]  [Web Applications]  [API Controllers]        │
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

## Core Component Hierarchy

### 1. ChainSharp (Core Engine)

The foundation layer providing Railway Oriented Programming patterns.

#### Key Classes

```csharp
// Base workflow class
public abstract class Workflow<TIn, TOut>
{
    public abstract Task<Either<Exception, TOut>> Run(TIn input);
    
    protected Chain<TIn> Activate(TIn input) => new Chain<TIn>(input);
}

// Step interface for individual operations
public interface IStep<TIn, TOut>
{
    Task<Either<Exception, TOut>> Run(TIn input);
}

// Chain class for composing steps
public class Chain<T>
{
    public Chain<TOut> Chain<TStep, TOut>() where TStep : IStep<T, TOut>;
    public Task<Either<Exception, T>> Resolve();
}
```

#### Responsibilities
- Define the Railway Oriented Programming pattern
- Provide step chaining mechanisms
- Handle error propagation through chains
- Core workflow lifecycle management

### 2. ChainSharp.Effect (Enhanced Workflows)

Extends core workflows with dependency injection, metadata tracking, and effect management.

#### EffectWorkflow<TIn, TOut>

```csharp
public abstract class EffectWorkflow<TIn, TOut> : Workflow<TIn, TOut>, IEffectWorkflow<TIn, TOut>
{
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

#### Responsibilities
- Extend base workflows with effect tracking
- Manage dependency injection through [Inject] attributes
- Handle metadata lifecycle (creation, tracking, persistence)
- Coordinate effect providers through EffectRunner
- Provide error handling and logging integration

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
        
        // 3. Inject properties with [Inject] attribute
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

## 6. Package Domains

### Core (`ChainSharp`)

The foundation library providing Railway Oriented Programming patterns. Contains:
- **Workflow<TIn, TOut>**: Base class for defining a sequence of steps that process input to output
- **Step<TIn, TOut>**: Base class for individual units of work within a workflow
- **Chain**: Fluent API for composing steps together (`Activate(input).Chain<Step1>().Chain<Step2>().Resolve()`)

Error handling is built-in using `Either<Exception, T>` monads from LanguageExt—if any step fails, subsequent steps are automatically short-circuited.

### Effect (`ChainSharp.Effect`)

Extends the core workflow with "effects" (side effects like logging, persistence, and metadata tracking). Contains:
- **EffectWorkflow<TIn, TOut>**: Enhanced workflow with automatic metadata tracking, dependency injection via `[Inject]` attributes, and effect coordination
- **EffectRunner**: Coordinates multiple effect providers and manages their lifecycle
- **IEffectProvider**: Interface for pluggable providers that react to workflow events (track models, save changes)

### Mediator (`ChainSharp.Effect.Mediator`)

Implements the mediator pattern for workflow discovery and execution. Contains:
- **WorkflowBus**: Discovers and executes workflows based on input type. Allows running workflows from controllers, services, or other workflows
- **WorkflowRegistry**: Scans assemblies at startup to build a mapping of input types → workflow types

> **Note**: Each input type can only map to ONE workflow. Use distinct input types (e.g., `CreateUserRequest`, `UpdateUserRequest`) rather than sharing types across workflows.

### Data (`ChainSharp.Effect.Data`)

Abstract data persistence layer for storing workflow metadata (execution state, timing, inputs/outputs, errors). Contains:
- **DataContext<T>**: Base EF Core DbContext with `Metadata`, `Log`, and `Manifest` entities
- **IDataContext**: Interface for database operations and transaction management

### Provider (`ChainSharp.Effect.Provider.*`)

Effect providers are pluggable components that react to workflow lifecycle events. They implement `IEffectProvider` and are registered via dependency injection.

#### Provider.Json

Debug logging provider that serializes tracked models to JSON and logs state changes. Useful for development and debugging workflow state.

#### Provider.Parameter

Serializes workflow input/output parameters to JSON for database storage. Enables querying workflow history by parameter values and provides an audit trail.

### StepProvider (`ChainSharp.Effect.StepProvider.*`)

Step-level effect providers that operate on individual steps within a workflow.

#### StepProvider.Logging

Provides structured logging for individual steps, capturing step inputs, outputs, and timing information.
