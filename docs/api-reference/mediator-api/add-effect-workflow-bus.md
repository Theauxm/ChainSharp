---
layout: default
title: AddEffectWorkflowBus (Registration)
parent: Mediator API
grand_parent: API Reference
nav_order: 2
---

# AddEffectWorkflowBus (Registration)

Registers the workflow bus and discovers all workflow implementations via assembly scanning. This is the same method as documented in [Configuration > AddEffectWorkflowBus]({{ site.baseurl }}{% link api-reference/configuration/add-effect-workflow-bus.md %}) — this page focuses on the registration and discovery behavior.

## How Discovery Works

1. Scans the specified assemblies for all types implementing `IEffectWorkflow<TIn, TOut>`.
2. For each discovered type, extracts the `TIn` (input) type.
3. Registers the workflow in the `IWorkflowRegistry` keyed by `TIn`.
4. Registers the workflow in the DI container with the specified lifetime.

## Input Type Uniqueness

Each input type must map to **exactly one** workflow. If two workflows accept the same `TIn`, registration will throw an exception.

```csharp
// This is fine — different input types
public class CreateOrderWorkflow : EffectWorkflow<CreateOrderInput, OrderResult> { }
public class CancelOrderWorkflow : EffectWorkflow<CancelOrderInput, OrderResult> { }

// This will FAIL — both accept OrderInput
public class CreateOrderWorkflow : EffectWorkflow<OrderInput, OrderResult> { }
public class UpdateOrderWorkflow : EffectWorkflow<OrderInput, OrderResult> { }
```

## Lifetime Considerations

| Lifetime | When to Use |
|----------|-------------|
| `Transient` (default) | Most workflows — each execution gets a fresh instance |
| `Scoped` | When the workflow needs to share state with other scoped services in the same request |
| `Singleton` | Rarely appropriate — workflows typically have per-execution state |

## Example

```csharp
services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(
        effectWorkflowServiceLifetime: ServiceLifetime.Transient,
        assemblies: typeof(Program).Assembly, typeof(SharedWorkflows).Assembly)
);
```

## Remarks

- The assembly scanning uses reflection to find `IEffectWorkflow<,>` implementations. Ensure the assemblies containing your workflows are passed to the `assemblies` parameter.
- Workflows registered here are available both through `IWorkflowBus.RunAsync` and through the scheduler system.
