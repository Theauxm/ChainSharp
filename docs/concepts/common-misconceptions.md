---
layout: default
title: Common Misconceptions
parent: Core Concepts
nav_order: 2
---

# Common Misconceptions

These are things developers hit immediately when starting with ChainSharp.

## ❌ Misconception 1: Steps need the `[Inject]` attribute for dependency injection

**This is FALSE.** The `[Inject]` attribute is used **internally** by the `EffectWorkflow` base class for its own framework-level services (`IEffectRunner`, `ILogger`, `IServiceProvider`).

**You do NOT need to use `[Inject]` anywhere in your workflow or step code.** Steps should use standard constructor injection for their dependencies:

```csharp
// ❌ WRONG - Don't use [Inject] in your steps
public class MyStep : Step<Input, Output>
{
    [Inject]
    public IMyService MyService { get; set; }  // Don't do this!

    public override async Task<Output> Run(Input input) { ... }
}

// ✅ CORRECT - Use constructor injection
public class MyStep(IMyService MyService) : Step<Input, Output>
{
    public override async Task<Output> Run(Input input)
    {
        // Use MyService directly - it's injected via constructor
        var result = await MyService.DoSomethingAsync(input);
        return result;
    }
}
```

## ❌ Misconception 2: Steps must "pass through" their input type as the output

**This is FALSE.** You do NOT need to return the same type you receive as input. The workflow's **Memory** system stores references to all objects, and these references are automatically updated.

```csharp
// ❌ WRONG - Unnecessarily passing through the User type
public class UpdateUserStep : Step<User, User>  // Don't do this!
{
    public override async Task<User> Run(User input)
    {
        input.LastModified = DateTime.UtcNow;
        return input;  // Unnecessarily returning the same object
    }
}

// ✅ CORRECT - The reference in Memory is already updated
public class UpdateUserStep : Step<User, Unit>  // This is correct!
{
    public override async Task<Unit> Run(User input)
    {
        input.LastModified = DateTime.UtcNow;
        // No need to return the User - the reference in Memory is already updated
        return Unit.Default;
    }
}
```

**Why does this work?** ChainSharp's Memory stores objects by their type. When you modify an object (like `User`), you're modifying the same reference that's stored in Memory. Subsequent steps that need the `User` will automatically get the updated object.

```csharp
// Example workflow demonstrating Memory reference behavior
public class ProcessUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<CreateUserStep>()      // Returns User, stored in Memory
            .Chain<ValidateUserStep>()    // Takes User, returns Unit (validation only)
            .Chain<EnrichUserStep>()      // Takes User, returns Unit (modifies in place)
            .Chain<SendNotificationStep>()// Takes User, returns Unit (side effect)
            .Resolve();                   // Resolves the User from Memory
}

// Each step receives the same User reference from Memory
public class ValidateUserStep(IValidator Validator) : Step<User, Unit>
{
    public override async Task<Unit> Run(User user)
    {
        if (!Validator.IsValid(user))
            throw new ValidationException("Invalid user");
        return Unit.Default;  // User is still available in Memory for next steps
    }
}

public class EnrichUserStep(IEnrichmentService Enricher) : Step<User, Unit>
{
    public override async Task<Unit> Run(User user)
    {
        user.EnrichedData = await Enricher.GetDataAsync(user.Id);
        // The modification is reflected in Memory automatically
        return Unit.Default;
    }
}
```

## Key Takeaways

1. **Constructor injection for steps** - Use primary constructors or standard constructor DI, not `[Inject]` attributes
2. **Memory handles references** - Objects in workflow Memory are references; modifications are automatically reflected
3. **Return meaningful types** - Only return a type from a step if it's a NEW type being added to Memory, not the same type you received
