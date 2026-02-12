---
layout: default
title: Steps
parent: Usage Guide
nav_order: 1
---

# Steps

Steps are the building blocks of workflows. Each step does one thing:

```csharp
public class ValidateEmailStep(IUserRepository UserRepository) : Step<CreateUserRequest, Unit>
{
    public override async Task<Unit> Run(CreateUserRequest input)
    {
        var existingUser = await UserRepository.GetByEmailAsync(input.Email);
        if (existingUser != null)
            throw new ValidationException($"Email {input.Email} already exists");

        return Unit.Default;
    }
}

public class CreateUserStep(IUserRepository UserRepository) : Step<CreateUserRequest, User>
{
    public override async Task<User> Run(CreateUserRequest input)
    {
        var user = new User
        {
            Email = input.Email,
            FullName = $"{input.FirstName} {input.LastName}",
            CreatedAt = DateTime.UtcNow
        };

        return await UserRepository.CreateAsync(user);
    }
}
```

Steps use constructor injection for dependencies. When a step throws, the workflow stops and returns the exception.

## EffectStep vs Step

ChainSharp has two step base classes:

**`Step<TIn, TOut>`** — The base class. Handles input/output and Railway error propagation. No metadata, no lifecycle hooks. Use this for lightweight steps or when running inside a plain `Workflow`.

**`EffectStep<TIn, TOut>`** — Extends `Step` with per-step metadata tracking. When run inside an `EffectWorkflow`, it records a `StepMetadata` entry with the step's name, input/output types, start/end times, and Railway state. Step effect providers (like `AddStepLogger`) hook into `EffectStep`'s lifecycle—they fire before and after each step executes.

```csharp
// Base step — no metadata tracking
public class ValidateEmailStep(IUserRepository repo) : Step<CreateUserRequest, Unit>
{
    public override async Task<Unit> Run(CreateUserRequest input)
    {
        if (await repo.GetByEmailAsync(input.Email) != null)
            throw new ValidationException("Email already exists");
        return Unit.Default;
    }
}

// Effect step — tracked by step effect providers
public class ValidateEmailStep(IUserRepository repo) : EffectStep<CreateUserRequest, Unit>
{
    public override async Task<Unit> Run(CreateUserRequest input)
    {
        if (await repo.GetByEmailAsync(input.Email) != null)
            throw new ValidationException("Email already exists");
        return Unit.Default;
    }
}
```

The implementation is identical—just swap the base class. `EffectStep` only adds metadata when running inside an `EffectWorkflow`. If you use `EffectStep` inside a plain `Workflow`, it throws at runtime.

Use `EffectStep` when you want step-level observability (timing, logging via `AddStepLogger`). Use `Step` when you don't need it.
