---
layout: default
title: ShortCircuit
parent: Workflow Methods
grand_parent: API Reference
nav_order: 4
---

# ShortCircuit

Executes a step that can **return early** from the workflow. If the step succeeds and returns a value of type `TReturn`, that value is captured as the short-circuit result — when [Resolve]({% link api-reference/workflow-methods/resolve.md %}) is called, it returns this value instead of looking in Memory, bypassing all remaining steps.

There are also `ShortCircuitChain` overloads that simply **ignore failures** (the step's Left/exception result is discarded and the workflow continues).

## Signatures

### ShortCircuit\<TStep\>()

Creates and executes a step with short-circuit behavior.

```csharp
public Workflow<TInput, TReturn> ShortCircuit<TStep>() where TStep : class
```

### ShortCircuit\<TStep\>(TStep stepInstance)

Executes a pre-created step with short-circuit behavior.

```csharp
public Workflow<TInput, TReturn> ShortCircuit<TStep>(TStep stepInstance) where TStep : class
```

### ShortCircuitChain\<TStep, TIn, TOut\>(TStep step, TIn previousStep, out Either\<Exception, TOut\> outVar)

Executes a step but **ignores failures**. Only stores output in Memory on success.

```csharp
public Workflow<TInput, TReturn> ShortCircuitChain<TStep, TIn, TOut>(
    TStep step,
    TIn previousStep,
    out Either<Exception, TOut> outVar
) where TStep : IStep<TIn, TOut>
```

## Example

```csharp
protected override async Task<Either<Exception, OrderResult>> RunInternal(OrderInput input)
{
    return Activate(input)
        .ShortCircuit<CheckCache>()       // If cache has result, return it immediately
        .Chain<ValidateOrder>()           // Only runs if cache miss
        .Chain<ProcessPayment>()          // Only runs if cache miss
        .Resolve();                       // Returns cached result OR processed result
}
```

## Behavior

### ShortCircuit\<TStep\>()
1. Creates the step instance and extracts input from Memory.
2. Executes the step via `ShortCircuitChain`.
3. If the step **succeeds** and returns a value of type `TReturn`:
   - The value is stored as `ShortCircuitValue`.
   - `Resolve()` will return this value, bypassing Memory lookup.
4. If the step **fails** (returns Left): the failure is **ignored** — no exception is set, the workflow continues normally.

### ShortCircuitChain\<TStep, TIn, TOut\>()
1. If there's already an exception, short-circuits.
2. Executes the step.
3. On **success** (Right): stores the output in Memory.
4. On **failure** (Left): the failure is **discarded** — the workflow continues without setting an exception.

## Remarks

- The key difference from `Chain`: failures do not stop the workflow. A failing short-circuit step is silently ignored.
- If the step output type matches the workflow's `TReturn`, the value becomes the short-circuit result for `Resolve()`.
- This is useful for cache checks, optional enrichment steps, and conditional early returns.
