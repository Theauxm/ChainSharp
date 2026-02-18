---
layout: default
title: Run / RunEither
parent: Workflow Methods
grand_parent: API Reference
nav_order: 8
---

# Run / RunEither

Executes the workflow from the outside. `Run` throws on failure; `RunEither` returns an `Either<Exception, TReturn>` for Railway-oriented error handling.

These are called by **consumers** of the workflow, not inside `RunInternal`.

## Signatures

### Run (throws on failure)

```csharp
public async Task<TReturn> Run(TInput input)
public async Task<TReturn> Run(TInput input, IServiceProvider serviceProvider)
```

### RunEither (returns Either)

```csharp
public Task<Either<Exception, TReturn>> RunEither(TInput input)
public Task<Either<Exception, TReturn>> RunEither(TInput input, IServiceProvider serviceProvider)
```

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `input` | `TInput` | Yes | The input data for the workflow |
| `serviceProvider` | `IServiceProvider` | No | A service provider for DI within the workflow. When provided, it's stored in Memory under `typeof(IServiceProvider)`. |

## Returns

- **`Run`**: `Task<TReturn>` — the workflow result. Throws the captured exception if the workflow failed.
- **`RunEither`**: `Task<Either<Exception, TReturn>>` — `Left` on failure, `Right` on success. Never throws.

## Examples

### Using Run (imperative style)

```csharp
try
{
    var result = await workflow.Run(new OrderInput { OrderId = "123" });
    Console.WriteLine($"Order processed: {result.ConfirmationId}");
}
catch (Exception ex)
{
    Console.WriteLine($"Workflow failed: {ex.Message}");
}
```

### Using RunEither (functional style)

```csharp
var result = await workflow.RunEither(new OrderInput { OrderId = "123" });

result.Match(
    Right: success => Console.WriteLine($"Order processed: {success.ConfirmationId}"),
    Left: error => Console.WriteLine($"Workflow failed: {error.Message}")
);
```

### With ServiceProvider

```csharp
var result = await workflow.Run(input, serviceProvider);
// Steps can now resolve IServiceProvider from Memory
```

## Behavior

1. Initializes `Memory` with `Unit.Default` (and optionally `IServiceProvider`).
2. Calls `RunInternal(input)` — the user-implemented method.
3. **`Run`**: Unwraps the `Either` result. If `Left`, rethrows the exception. If `Right`, returns the value.
4. **`RunEither`**: Returns the `Either` directly without unwrapping.

## Remarks

- `RunEither` is useful when you want functional-style error handling without try/catch. It pairs naturally with LanguageExt's `Match`, `Map`, `Bind`, etc.
- In most applications, workflows are executed through `IWorkflowBus.RunAsync` (which calls `Run` internally) rather than calling `Run` directly. See [WorkflowBus]({% link api-reference/mediator-api/workflow-bus.md %}).
- The `serviceProvider` overload enables steps to resolve additional services beyond what was placed in Memory via `AddServices`.
