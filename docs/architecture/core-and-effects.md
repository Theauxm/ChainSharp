---
layout: default
title: Core & Effects
parent: Architecture
nav_order: 1
---

# Core & Effect System

## ChainSharp (Core Engine)

The foundation layer providing Railway Oriented Programming patterns.

### Key Classes

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

## ChainSharp.Effect (Enhanced Workflows)

Extends core workflows with dependency injection, metadata tracking, and effect management.

### EffectWorkflow<TIn, TOut>

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

### EffectRunner

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

## Effect Providers Architecture

Effect providers implement the `IEffectProvider` interface to handle different concerns.

### IEffectProvider Interface

```csharp
public interface IEffectProvider : IDisposable
{
    Task SaveChanges(CancellationToken cancellationToken);
    Task Track(IModel model);
}
```

### Available Effect Providers

| Provider | Package | Purpose | Performance Impact |
|----------|---------|---------|-------------------|
| **DataContext** | ChainSharp.Effect.Data | Database persistence | Medium - Database I/O |
| **JsonEffect** | ChainSharp.Effect.Provider.Json | Debug logging | Low - JSON serialization |
| **ParameterEffect** | ChainSharp.Effect.Provider.Parameter | Parameter serialization | Medium - JSON + Storage |
| **Custom Providers** | User-defined | Application-specific | Varies |

## Execution Flow

The full lifecycle of an `EffectWorkflow` execution, corresponding to the `Run` method shown above:

```
[Client Request]
       │
       ▼
[WorkflowBus.RunAsync]
       │
       ▼
[Find Workflow by Input Type]
       │
       ▼
[Create Workflow Instance]
       │
       ▼
[Inject Dependencies]
       │
       ▼
[Initialize Metadata]
       │
       ▼
[Execute Workflow Chain]
       │
       ▼
   Success? ──No──► [Update Metadata: Failed]
       │                      │
      Yes                     │
       │                      │
       ▼                      ▼
[Update Metadata: Completed]  │
       │                      │
       └──────────┬───────────┘
                  │
                  ▼
       [SaveChanges - Execute Effects]
                  │
                  ▼
           [Return Result]
```

Steps execute inside the "Execute Workflow Chain" box. The `SaveChanges` call at the end triggers all registered effect providers—database persistence, JSON logging, parameter serialization—to execute their accumulated side effects. Both success and failure paths call `SaveChanges`, so metadata is always persisted regardless of outcome.
