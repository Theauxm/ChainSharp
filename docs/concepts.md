---
layout: default
title: Core Concepts
nav_order: 3
---

# Core Concepts

This document explains the fundamental concepts that power ChainSharp. Understanding these concepts is crucial for working with any part of the system.

## 1. Railway Oriented Programming (ROP)

### What is Railway Oriented Programming?

Railway Oriented Programming is a functional programming pattern that treats operations like a train on railway tracks:

- **Success Track**: Operations continue flowing forward when successful
- **Failure Track**: Operations jump to the failure track when errors occur
- **No Manual Error Checking**: The "railway" automatically routes success/failure

```
Input → [Step 1] → [Step 2] → [Step 3] → Output
            ↓          ↓          ↓
         [Error] → [Error] → [Error] → Final Error
```

### Railway Pattern in C#

ChainSharp uses the `Either<Exception, T>` type to represent railway tracks:

```csharp
public class Either<TLeft, TRight>
{
    // Contains either an error (TLeft) or success value (TRight)
    public bool IsLeft { get; }     // Error occurred
    public bool IsRight { get; }    // Success occurred
}

// Usage in workflows:
public async Task<Either<Exception, User>> CreateUser(CreateUserRequest input)
{
    return Activate(input)
        .Chain<ValidateUserStep>()    // If this fails, skip remaining steps
        .Chain<CreateUserStep>()      // Only runs if validation succeeded
        .Chain<SendEmailStep>()       // Only runs if creation succeeded
        .Resolve();                   // Return Either<Exception, User>
}
```

### Benefits of Railway Pattern

1. **No Forgotten Error Handling**: Errors automatically propagate
2. **Clean Success Path**: Focus on happy path without try/catch blocks
3. **Composable Operations**: Chain operations without nested error checking
4. **Type Safety**: Compiler ensures you handle both success and failure cases

## 2. Effect Pattern

### What is the Effect Pattern?

The Effect Pattern separates **describing what to do** from **actually doing it**:

- **Track Phase**: Record what effects need to happen
- **Execute Phase**: Run all tracked effects atomically

This is similar to how Entity Framework's DbContext works:

```csharp
// Track changes (doesn't hit database yet)
context.Users.Add(user);
context.Orders.Update(order);

// Execute all changes atomically
await context.SaveChanges();
```

### Effect Pattern in ChainSharp

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

### Why Use the Effect Pattern?

1. **Consistency**: All effects succeed or fail together
2. **Performance**: Batch operations instead of individual database calls
3. **Testability**: Can verify what effects would happen without executing them
4. **Debugging**: See exactly what will happen before it happens

## 3. Core Components Explained

### Workflows vs Steps vs Effects

```
┌─────────────────────────────────────────────────────────┐
│                    Workflow Level                       │
│  ┌─────────────────────────────────────────────────┐   │
│  │ EffectWorkflow → Orchestrates Steps             │   │
│  │              → Manages Effects                  │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                     Step Level                          │
│  [Step 1: Validate] → [Step 2: Process] → [Step 3]     │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                    Effect Level                         │
│  [Database Effect] [JSON Log Effect] [Parameter Effect] │
└─────────────────────────────────────────────────────────┘
```

#### Workflows
- **Purpose**: Orchestrate business processes
- **Responsibility**: Chain steps together, handle dependencies
- **Example**: CreateUserWorkflow, ProcessOrderWorkflow

#### Steps  
- **Purpose**: Perform specific operations
- **Responsibility**: Single, focused task
- **Example**: ValidateEmailStep, SendNotificationStep

#### Effects
- **Purpose**: Handle cross-cutting concerns
- **Responsibility**: Persistence, logging, notifications
- **Example**: Database persistence, JSON logging, parameter serialization

### The EffectRunner

The EffectRunner coordinates all effects in a workflow:

```csharp
public class EffectRunner : IEffectRunner
{
    private List<IEffectProvider> ActiveEffectProviders { get; }

    public async Task Track(IModel model)
    {
        // Send model to all effect providers
        foreach (var provider in ActiveEffectProviders)
        {
            await provider.Track(model);
        }
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        // Execute all tracked effects atomically
        foreach (var provider in ActiveEffectProviders)
        {
            await provider.SaveChanges(cancellationToken);
        }
    }
}
```

## 4. Metadata Tracking and Lifecycle

### What is Metadata?

Metadata is a record of workflow execution containing:

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

### Metadata States

| State | Meaning | When Set |
|-------|---------|----------|
| **Pending** | Workflow created but not started | Metadata.Create() |
| **InProgress** | Workflow is executing | Workflow.Run() starts |
| **Completed** | Workflow finished successfully | Workflow.Run() succeeds |
| **Failed** | Workflow encountered an error | Workflow.Run() fails |

### Parent-Child Relationships

Workflows can spawn child workflows:

```csharp
public class ParentWorkflow(IWorkflowBus WorkflowBus) : EffectWorkflow<ParentRequest, ParentResult>
{
    protected override async Task<Either<Exception, ParentResult>> RunInternal(ParentRequest input)
    {
        // Pass current metadata as parent for child workflow
        var childResult = await WorkflowBus.RunAsync<ChildResult>(
            new ChildRequest(), 
            Metadata  // This creates parent-child relationship
        );
        
        return new ParentResult { ChildData = childResult };
    }
}
```

## Understanding ChainSharp Flow

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
