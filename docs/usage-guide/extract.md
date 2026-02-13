---
layout: default
title: Extract
parent: Usage Guide
nav_order: 6
---

# Extract

`.Extract<TSource, TTarget>()` pulls a nested value out of an object in [Memory](../concepts/memory.md). It finds the `TSource` object, looks for a property or field of type `TTarget`, and stores that value in Memory under the `TTarget` type.

```csharp
Activate(input)
    .Chain<LoadUserStep>()              // Returns User, stored in Memory
    .Extract<User, EmailAddress>()      // Finds EmailAddress property on User, stores it
    .Chain<ValidateEmailStep>()         // Takes EmailAddress from Memory
    .Resolve();
```

## How It Works

`Extract` uses reflection to scan the `TSource` type for a property or field whose type matches `TTarget`. If it finds one, it reads the value and adds it to Memory. If it doesn't find a match, the workflow fails.

```csharp
public record User
{
    public string Name { get; init; }
    public EmailAddress Email { get; init; }  // ← Extract<User, EmailAddress> finds this
    public Address HomeAddress { get; init; }
}
```

## When to Use It

`Extract` is a convenience for avoiding a step that exists solely to pull a property off an object. Without it, you'd write:

```csharp
public class GetUserEmailStep : Step<User, EmailAddress>
{
    public override Task<EmailAddress> Run(User input)
        => Task.FromResult(input.Email);
}
```

`.Extract<User, EmailAddress>()` does the same thing without the boilerplate.

Use it when the property access is trivial. If you need any logic (null checking, transformation, validation), write a step instead.
