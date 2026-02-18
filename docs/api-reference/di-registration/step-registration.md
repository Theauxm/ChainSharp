---
layout: default
title: Step Registration
parent: DI Registration
grand_parent: API Reference
nav_order: 2
---

# Step Registration

Extension methods for registering ChainSharp steps with `[Inject]` property injection support. These are **aliases** for the corresponding [Workflow Registration]({{ site.baseurl }}{% link api-reference/di-registration/workflow-registration.md %}) methods — the injection behavior is identical.

## Signatures

### Generic Overloads

```csharp
public static IServiceCollection AddScopedChainSharpStep<TService, TImplementation>(
    this IServiceCollection services
) where TService : class where TImplementation : class, TService

public static IServiceCollection AddTransientChainSharpStep<TService, TImplementation>(
    this IServiceCollection services
) where TService : class where TImplementation : class, TService

public static IServiceCollection AddSingletonChainSharpStep<TService, TImplementation>(
    this IServiceCollection services
) where TService : class where TImplementation : class, TService
```

### Non-Generic Overloads

```csharp
public static IServiceCollection AddScopedChainSharpStep(
    this IServiceCollection services, Type serviceInterface, Type serviceImplementation
)

public static IServiceCollection AddTransientChainSharpStep(
    this IServiceCollection services, Type serviceInterface, Type serviceImplementation
)

public static IServiceCollection AddSingletonChainSharpStep(
    this IServiceCollection services, Type serviceInterface, Type serviceImplementation
)
```

## Example

```csharp
services.AddScopedChainSharpStep<IValidateOrderStep, ValidateOrderStep>();
services.AddTransientChainSharpStep<IProcessPaymentStep, ProcessPaymentStep>();
```

## Remarks

- These methods delegate directly to the workflow registration equivalents. They exist for semantic clarity — `AddChainSharpStep` communicates intent better than `AddChainSharpWorkflow` when registering steps.
- Steps typically don't need manual DI registration unless they use `[Inject]` properties. Most steps are created by the workflow's `Chain<TStep>()` method using Memory-based constructor injection.
