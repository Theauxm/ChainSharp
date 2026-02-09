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

### Tuple Handling in Memory

ChainSharp's Memory system has built-in support for **tuples**. Tuples are automatically constructed and deconstructed, allowing steps to work with multiple types seamlessly.

#### Tuple Inputs: Automatic Construction

When a step requires a tuple input like `Step<(User, Admin), Unit>`, the Memory system will:
1. Find the `User` **individually** in Memory
2. Find the `Admin` **individually** in Memory  
3. Construct the tuple `(User, Admin)` and pass it to the step

```csharp
// Step that requires both User and Admin from Memory
public class GrantPermissionsStep(IPermissionService PermissionService) : Step<(User, Admin), Unit>
{
    public override async Task<Unit> Run((User User, Admin Admin) input)
    {
        // Both User and Admin are found individually in Memory and combined into the tuple
        await PermissionService.GrantAsync(input.User, input.Admin);
        return Unit.Default;
    }
}

// Workflow demonstrating tuple input construction
public class PermissionWorkflow : EffectWorkflow<PermissionRequest, Unit>
{
    protected override async Task<Either<Exception, Unit>> RunInternal(PermissionRequest input)
        => Activate(input)
            .Chain<LoadUserStep>()         // Returns User, stored in Memory
            .Chain<LoadAdminStep>()        // Returns Admin, stored in Memory
            .Chain<GrantPermissionsStep>() // Takes (User, Admin) - constructed from Memory!
            .Resolve();
}
```

#### Tuple Outputs: Automatic Deconstruction

When a step returns a tuple like `Step<Unit, (User, Admin)>`, the Memory system will:
1. Deconstruct the tuple into its components
2. Store `User` **individually** in Memory
3. Store `Admin` **individually** in Memory

```csharp
// Step that returns multiple objects at once
public class LoadUserAndAdminStep(IRepository Repository) : Step<LoadRequest, (User, Admin)>
{
    public override async Task<(User, Admin)> Run(LoadRequest input)
    {
        var user = await Repository.GetUserAsync(input.UserId);
        var admin = await Repository.GetAdminAsync(input.AdminId);
        
        // Return tuple - Memory will deconstruct and store User and Admin separately
        return (user, admin);
    }
}

// Workflow demonstrating tuple output deconstruction
public class ProcessWorkflow : EffectWorkflow<ProcessRequest, Result>
{
    protected override async Task<Either<Exception, Result>> RunInternal(ProcessRequest input)
        => Activate(input)
            .Chain<LoadUserAndAdminStep>() // Returns (User, Admin) - deconstructed into Memory!
            .Chain<ValidateUserStep>()     // Takes User - available from Memory
            .Chain<ValidateAdminStep>()    // Takes Admin - available from Memory
            .Chain<ProcessStep>()          // Takes (User, Admin) - reconstructed from Memory!
            .Resolve();
}
```

#### Tuple Limitations

- **Maximum 7 elements**: Tuples can have at most 7 elements
- **Unique types**: Each type in the tuple must be unique (you cannot have `(User, User)`)
- **Non-null**: All tuple elements must be non-null

```csharp
// ✅ Valid - all unique types
public class MyStep : Step<(User, Admin, Order), Unit> { }

// ❌ Invalid - duplicate types
public class MyStep : Step<(User, User), Unit> { }  // Won't work - can't distinguish between Users

// ❌ Invalid - too many elements
public class MyStep : Step<(A, B, C, D, E, F, G, H), Unit> { }  // Max 7 elements
```

### Key Takeaways

1. **Constructor injection for steps** - Use primary constructors or standard constructor DI, not `[Inject]` attributes
2. **Memory handles references** - Objects in workflow Memory are references; modifications are automatically reflected
3. **Return meaningful types** - Only return a type from a step if it's a NEW type being added to Memory, not the same type you received
4. **Tuples are auto-constructed/deconstructed** - Use tuples to work with multiple types; Memory handles the construction and deconstruction automatically
