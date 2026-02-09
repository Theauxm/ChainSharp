---
layout: default
title: Core Concepts
nav_order: 3
---

# Core Concepts

## Railway Oriented Programming

Railway Oriented Programming comes from functional programming. The idea: your code has two tracks, success and failure. Each operation either continues down the success track or switches to the failure track

```
Input → [Step 1] → [Step 2] → [Step 3] → Output
            ↓          ↓          ↓
         [Error] → [Error] → [Error] → Final Error
```

ChainSharp uses `Either<Exception, T>` from LanguageExt to represent this. A value is either `Left` (an exception) or `Right` (the success value):

```csharp
public async Task<Either<Exception, User>> CreateUser(CreateUserRequest input)
{
    return Activate(input)
        .Chain<ValidateUserStep>()    // If this fails, skip remaining steps
        .Chain<CreateUserStep>()      // Only runs if validation succeeded
        .Chain<SendEmailStep>()       // Only runs if creation succeeded
        .Resolve();                   // Return Either<Exception, User>
}
```

If `ValidateUserStep` throws, the workflow immediately returns `Left(exception)`. `CreateUserStep` and `SendEmailStep` never execute. You don't write any error-checking code—the chain handles it.

## The Effect Pattern

The Effect Pattern separates *describing* what should happen from *doing* it. If you've used Entity Framework, you already know this pattern:

```csharp
// Track changes (doesn't hit database yet)
context.Users.Add(user);
context.Orders.Update(order);

// Execute all changes atomically
await context.SaveChanges();
```

ChainSharp's `EffectWorkflow` does the same thing. Steps can track models, log entries, and other effects. Nothing actually persists until the workflow completes successfully and calls `SaveChanges`. If any step fails, nothing is saved.

This gives you atomic workflows—either everything succeeds and all effects are applied, or something fails and nothing is applied.

## Workflow vs EffectWorkflow

ChainSharp has two base classes for workflows:

**`Workflow<TIn, TOut>`** — The core class. Handles chaining, Memory, and error propagation. No metadata, no effects, no automatic dependency injection from an `IServiceProvider`. Use this when:
- You want a lightweight workflow without persistence
- You're composing steps inside a larger system that handles its own concerns
- Testing or prototyping

```csharp
public class SimpleUserCreation : Workflow<CreateUserRequest, User>
{
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Resolve();
}
```

**`EffectWorkflow<TIn, TOut>`** — Extends `Workflow` with:
- Automatic metadata tracking (start time, end time, success/failure, inputs/outputs)
- Effect providers (database persistence, JSON logging, parameter serialization)
- Integration with `IWorkflowBus` for workflow discovery
- `IServiceProvider` access for step instantiation

Use this when you want observability, persistence, or the mediator pattern:

```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Resolve();
}
```

Most applications should use `EffectWorkflow`. The base `Workflow` exists for cases where you need the chaining pattern without the infrastructure.

## Workflows, Steps, and Effects

```
┌─────────────────────────────────────────────────────────┐
│                       Workflow                          │
│         Orchestrates steps, manages effects             │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                        Steps                            │
│  [Validate] ────► [Create] ────► [Notify]              │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                       Effects                           │
│     Database    │    JSON Log    │    Parameters        │
└─────────────────────────────────────────────────────────┘
```

A **workflow** is a sequence of steps that accomplish a business operation. `CreateUserWorkflow` chains together validation, database insertion, and email notification.

A **step** does one thing. `ValidateEmailStep` checks if an email is valid. `CreateUserInDatabaseStep` inserts a record. Steps are easy to test in isolation because they have a single responsibility.

**Effects** are cross-cutting concerns that happen as a result of steps running—database writes, log entries, serialized parameters. Effect providers collect these during workflow execution and apply them atomically at the end.

## Metadata

Every workflow execution creates a metadata record:

```csharp
public class Metadata : IMetadata
{
    public int Id { get; }                          // Unique identifier
    public string Name { get; set; }                // Workflow name
    public WorkflowState WorkflowState { get; set; } // Pending/InProgress/Completed/Failed
    public DateTime StartTime { get; set; }         // When workflow started
    public DateTime? EndTime { get; set; }          // When workflow finished
    public JsonDocument? Input { get; set; }        // Serialized input
    public JsonDocument? Output { get; set; }       // Serialized output
    public string? FailureException { get; }        // Error details if failed
    public string? FailureReason { get; }           // Human-readable error
    public int? ParentId { get; set; }              // For nested workflows
}
```

The `WorkflowState` tracks progress: `Pending` → `InProgress` → `Completed` (or `Failed`). If a workflow fails, the metadata captures the exception, stack trace, and which step failed.

### Nested Workflows

Workflows can run other workflows. Pass the current `Metadata` to establish a parent-child relationship:

```csharp
public class ParentWorkflow(IWorkflowBus WorkflowBus) : EffectWorkflow<ParentRequest, ParentResult>
{
    protected override async Task<Either<Exception, ParentResult>> RunInternal(ParentRequest input)
    {
        var childResult = await WorkflowBus.RunAsync<ChildResult>(
            new ChildRequest(), 
            Metadata  // Child's ParentId will point to this workflow
        );
        
        return new ParentResult { ChildData = childResult };
    }
}
```

This creates a tree of metadata records you can query to trace execution across workflows.

## Execution Flow

```
[Client Request]
       │
       ▼
[WorkflowBus.RunAsync]
       │
       ▼
[Find Workflow by Input Type]
       │
       ▼
[Create Workflow Instance]
       │
       ▼
[Inject Dependencies]
       │
       ▼
[Initialize Metadata]
       │
       ▼
[Execute Workflow Chain]
       │
       ▼
   Success? ──No──► [Update Metadata: Failed]
       │                      │
      Yes                     │
       │                      │
       ▼                      ▼
[Update Metadata: Completed]  │
       │                      │
       └──────────┬───────────┘
                  │
                  ▼
       [SaveChanges - Execute Effects]
                  │
                  ▼
           [Return Result]
```

## Common Misconceptions

### ❌ Misconception 1: Steps need the `[Inject]` attribute for dependency injection

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

### ❌ Misconception 2: Steps must "pass through" their input type as the output

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
            .Resolve<User>();             // Resolves the User from Memory
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

### Key Takeaways

1. **Constructor injection for steps** - Use primary constructors or standard constructor DI, not `[Inject]` attributes
2. **Memory handles references** - Objects in workflow Memory are references; modifications are automatically reflected
3. **Return meaningful types** - Only return a type from a step if it's a NEW type being added to Memory, not the same type you received
