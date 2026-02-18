---
layout: default
title: Chain
parent: Workflow Methods
grand_parent: API Reference
nav_order: 2
---

# Chain

Executes a step, wiring its input from Memory and storing its output back into Memory. This is the primary method for composing steps into a workflow pipeline. If any step fails (returns `Left`), subsequent steps are short-circuited.

Chain has 12 overloads organized into three groups by type parameter count.

## Reflection-Based (1 Type Parameter)

The most common form. The step's `TIn`/`TOut` types are resolved via reflection from its `IStep<TIn, TOut>` implementation.

### Chain\<TStep\>()

Creates and executes a step. Input is auto-extracted from Memory.

```csharp
public Workflow<TInput, TReturn> Chain<TStep>() where TStep : class
```

| Type Parameter | Constraint | Description |
|---------------|------------|-------------|
| `TStep` | `class` | The step type. Must implement `IStep<TIn, TOut>` for some `TIn`/`TOut`. |

This is the overload used in most workflows:

```csharp
return Activate(input)
    .Chain<ValidateOrder>()     // Creates ValidateOrder, extracts its input from Memory
    .Chain<ProcessPayment>()    // Creates ProcessPayment, extracts its input from Memory
    .Resolve();
```

### Chain\<TStep\>(TStep stepInstance)

Executes a pre-created step instance. Input is auto-extracted from Memory.

```csharp
public Workflow<TInput, TReturn> Chain<TStep>(TStep stepInstance) where TStep : class
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `stepInstance` | `TStep` | A pre-created step instance |

Useful when you need to configure a step before executing it:

```csharp
var step = new ProcessPayment { Gateway = "stripe" };
return Activate(input)
    .Chain<ProcessPayment>(step)
    .Resolve();
```

---

## Fully-Typed (3 Type Parameters)

Explicit `TIn`/`TOut` type parameters. Used when you need direct control over input/output wiring.

### Chain\<TStep, TIn, TOut\>(TStep step, Either\<Exception, TIn\> previousStep, out Either\<Exception, TOut\> outVar)

The core method — all other overloads eventually delegate here. Executes a step with explicit input and captures the output.

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(
    TStep step,
    Either<Exception, TIn> previousStep,
    out Either<Exception, TOut> outVar
) where TStep : IStep<TIn, TOut>
```

### Chain\<TStep, TIn, TOut\>(TStep step)

Executes a step instance with input auto-extracted from Memory.

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step)
    where TStep : IStep<TIn, TOut>
```

### Chain\<TStep, TIn, TOut\>(Either\<Exception, TIn\> previousStep, out Either\<Exception, TOut\> outVar)

Creates a new step via `new()` and executes with explicit input.

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(
    Either<Exception, TIn> previousStep,
    out Either<Exception, TOut> outVar
) where TStep : IStep<TIn, TOut>, new()
```

### Chain\<TStep, TIn, TOut\>()

Creates a new step via `new()` with input auto-extracted from Memory.

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>()
    where TStep : IStep<TIn, TOut>, new()
```

---

## Unit-Returning Convenience (2 Type Parameters)

For steps that return `Unit` (void-equivalent). Wrappers around the 3-type-parameter overloads with `TOut = Unit`.

### Chain\<TStep, TIn\>(TStep step, Either\<Exception, TIn\> previousStep)

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn>(
    TStep step, Either<Exception, TIn> previousStep
) where TStep : IStep<TIn, Unit>
```

### Chain\<TStep, TIn\>(TStep step)

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step)
    where TStep : IStep<TIn, Unit>
```

### Chain\<TStep, TIn\>(Either\<Exception, TIn\> previousStep)

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn>(Either<Exception, TIn> previousStep)
    where TStep : IStep<TIn, Unit>, new()
```

### Chain\<TStep, TIn\>()

```csharp
public Workflow<TInput, TReturn> Chain<TStep, TIn>()
    where TStep : IStep<TIn, Unit>, new()
```

---

## Behavior

1. If the workflow already has an exception, the step is **skipped** (short-circuited).
2. The step's input is extracted from Memory by type.
3. The step is executed via `RailwayStep`.
4. On **success** (Right): the output is stored in Memory by its type. Tuple outputs are decomposed into individual Memory entries.
5. On **failure** (Left): the exception is captured and all subsequent Chain calls are short-circuited.

## Remarks

- Steps are created and injected with DI services from Memory via `InitializeStep`. Constructor parameters are resolved from Memory by type.
- The 1-type-parameter overloads use reflection to discover `TIn`/`TOut` from the step's `IStep<,>` interface at runtime.
- See [Memory]({% link concepts/memory.md %}) for how type-based lookup works, including tuple handling.
