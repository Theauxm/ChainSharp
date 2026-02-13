---
layout: default
title: Chain
parent: Usage Guide
nav_order: 3
---

# Chain

`.Chain<TStep>()` is the primary way to add a step to a workflow. It resolves the step from the DI container, pulls its input from [Memory](../concepts/memory.md), runs it, and stores the output back in Memory.

```csharp
Activate(input)
    .Chain<ValidateEmailStep>()
    .Chain<CreateUserStep>()
    .Chain<SendEmailStep>()
    .Resolve();
```

## How It Works

When the chain reaches `.Chain<TStep>()`:

1. The step's `TIn` type is looked up in Memory
2. The step is resolved from the DI container and executed with that value
3. The step's `TOut` is stored in Memory
4. If `TIn` is not in Memory, the workflow fails

The [Analyzer](../analyzer.md) catches missing types at compile time, so you'll see these errors in your IDE before you ever run the code.

## Railway Behavior

If a previous step threw an exception, `.Chain<TStep>()` is skipped entirely. The exception propagates through the chain until it reaches `.Resolve()`, which returns it as `Left(exception)`.

```csharp
Activate(input)
    .Chain<ValidateEmailStep>()    // Throws ValidationException
    .Chain<CreateUserStep>()       // Skipped
    .Chain<SendEmailStep>()        // Skipped
    .Resolve();                    // Returns Left(ValidationException)
```

This is the core of Railway Oriented Programming—see [Railway Programming](../concepts/railway-programming.md) for the full explanation.
