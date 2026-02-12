---
layout: default
title: Effect Providers
parent: Usage Guide
nav_order: 5
---

# Configuring Effect Providers

ChainSharp has several effect providers. Here's when to use each:

## Database Persistence (Postgres or InMemory)

**Use when:** You need to query workflow history, audit execution, or debug production issues.

```csharp
// Production
services.AddChainSharpEffects(options =>
    options.AddPostgresEffect("Host=localhost;Database=app;Username=postgres;Password=pass")
);

// Testing
services.AddChainSharpEffects(options =>
    options.AddInMemoryEffect()
);
```

This persists a `Metadata` record for each workflow execution containing:
- Workflow name and state (Pending → InProgress → Completed/Failed)
- Start and end timestamps
- Serialized input and output
- Exception details if failed
- Parent workflow ID for nested workflows

## JSON Effect (`AddJsonEffect`)

**Use when:** Debugging during development. Logs workflow state changes to your configured logger.

```csharp
services.AddChainSharpEffects(options =>
    options.AddJsonEffect()
);
```

This doesn't persist anything—it just logs. Useful for seeing what's happening without setting up a database.

## Parameter Effect (`SaveWorkflowParameters`)

**Use when:** You need to store workflow inputs/outputs in the database for later querying or replay.

```csharp
services.AddChainSharpEffects(options =>
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()  // Serializes Input/Output to Metadata
);
```

Without this, the `Input` and `Output` columns in `Metadata` are null. With it, they contain JSON-serialized versions of your request and response objects.

## Step Logger (`AddStepLogger`)

**Use when:** You want structured logging for individual step executions inside a workflow.

```csharp
services.AddChainSharpEffects(options =>
    options.AddStepLogger(serializeStepData: true)
);
```

This hooks into `EffectStep` (not base `Step`) lifecycle events. Before and after each step runs, it logs structured `StepMetadata` containing the step name, input/output types, timing, and Railway state (`Right`/`Left`). When `serializeStepData` is `true`, the step's output is also serialized to JSON in the log entry.

Requires steps to inherit from `EffectStep<TIn, TOut>` instead of `Step<TIn, TOut>`. See [EffectStep vs Step](steps.md#effectstep-vs-step).

## Combining Providers

Providers compose. A typical production setup:

```csharp
services.AddChainSharpEffects(options =>
    options
        .AddPostgresEffect(connectionString)   // Persist metadata
        .SaveWorkflowParameters()              // Include input/output in metadata
        .AddStepLogger(serializeStepData: true) // Log individual step executions
        .AddEffectWorkflowBus(assemblies)      // Enable workflow discovery
);
```

A typical development setup:

```csharp
services.AddChainSharpEffects(options =>
    options
        .AddInMemoryEffect()                   // Fast, no database needed
        .AddJsonEffect()                       // Log state changes
        .AddStepLogger()                       // Log step executions
        .AddEffectWorkflowBus(assemblies)
);
```
