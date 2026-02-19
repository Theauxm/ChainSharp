---
layout: default
title: IChain
parent: Workflow Methods
grand_parent: API Reference
nav_order: 3
---

# IChain

Resolves a step from Memory by its **interface type**, then executes it. Unlike `Chain<TStep>()` which creates a new step instance, `IChain<TStep>()` looks up an existing instance already stored in Memory (typically placed there via [AddServices]({{ site.baseurl }}{% link api-reference/workflow-methods/add-services.md %}) or [Activate]({{ site.baseurl }}{% link api-reference/workflow-methods/activate.md %})).

## Signature

```csharp
public Workflow<TInput, TReturn> IChain<TStep>() where TStep : class
```

## Type Parameters

| Type Parameter | Constraint | Description |
|---------------|------------|-------------|
| `TStep` | `class` | Must be an **interface** type. The step instance is resolved from Memory by this interface. Must implement `IStep<TIn, TOut>` for some `TIn`/`TOut`. |

## Returns

`Workflow<TInput, TReturn>` — the workflow instance, for fluent chaining.

## Example

```csharp
public class MyWorkflow(IMyStep myStep) : Workflow<MyInput, MyOutput>
{
    protected override async Task<Either<Exception, MyOutput>> RunInternal(MyInput input)
    {
        return Activate(input)
            .AddServices<IMyStep>(myStep)   // Store the step instance in Memory by interface
            .IChain<IMyStep>()              // Resolve from Memory by interface and execute
            .Resolve();
    }
}
```

## Behavior

1. Verifies that `TStep` is an interface. If not, sets the exception: `"Step ({TStep}) must be an interface to call IChain."`
2. Looks up the step instance in Memory by `typeof(TStep)`.
3. If not found, sets an exception and short-circuits.
4. Delegates to `Chain<TStep>(stepInstance)` for execution.

## Remarks

- Use `IChain` when you want to inject step implementations via DI and resolve them at runtime by interface. This enables swapping step implementations without changing the workflow.
- The step instance must already be in Memory before calling `IChain`. Use `AddServices` or `Activate` with extra inputs to place it there.
- For creating a new step instance (the common case), use [Chain\<TStep\>()]({{ site.baseurl }}{% link api-reference/workflow-methods/chain.md %}) instead.
