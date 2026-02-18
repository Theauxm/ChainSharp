---
layout: default
title: AddEffectWorkflowBus
parent: Configuration
grand_parent: API Reference
nav_order: 7
---

# AddEffectWorkflowBus

Registers the `IWorkflowBus` and `IWorkflowRegistry` services, and discovers all `IEffectWorkflow<,>` implementations via assembly scanning. This enables dynamic workflow dispatch by input type.

## Signature

```csharp
public static ChainSharpEffectConfigurationBuilder AddEffectWorkflowBus(
    this ChainSharpEffectConfigurationBuilder configurationBuilder,
    ServiceLifetime effectWorkflowServiceLifetime = ServiceLifetime.Transient,
    params Assembly[] assemblies
)
```

## Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `effectWorkflowServiceLifetime` | `ServiceLifetime` | No | `Transient` | The DI lifetime for discovered workflow registrations |
| `assemblies` | `params Assembly[]` | Yes | — | Assemblies to scan for `IEffectWorkflow<,>` implementations |

## Returns

`ChainSharpEffectConfigurationBuilder` — for continued fluent chaining.

## Example

```csharp
services.AddChainSharpEffects(options => options
    .AddPostgresEffect(connectionString)
    .AddEffectWorkflowBus(
        effectWorkflowServiceLifetime: ServiceLifetime.Scoped,
        assemblies: typeof(Program).Assembly)
);
```

## What It Registers

1. Scans the specified assemblies for all types implementing `IEffectWorkflow<TIn, TOut>`
2. Registers each discovered workflow with the DI container at the specified lifetime
3. Registers `IWorkflowBus` for dynamic workflow dispatch
4. Registers `IWorkflowRegistry` for workflow type lookup

## Remarks

- Each input type must map to **exactly one** workflow. If two workflows accept the same input type, registration will fail.
- The assembly scanning discovers workflows by their `IEffectWorkflow<TIn, TOut>` interface, not by convention.
- See [WorkflowBus]({% link api-reference/mediator-api/workflow-bus.md %}) for the runtime dispatch API.

## Package

Part of `ChainSharp.Effect.Orchestration.Mediator`.
