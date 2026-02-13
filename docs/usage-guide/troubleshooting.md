---
layout: default
title: Troubleshooting
parent: Usage Guide
nav_order: 10
---

# Troubleshooting

## "No workflow found for input type X"

The `WorkflowBus` couldn't find a workflow that accepts your input type.

**Causes:**
- The assembly containing your workflow wasn't registered with `AddEffectWorkflowBus`
- Your workflow doesn't implement `IEffectWorkflow<TIn, TOut>`
- Your workflow class is `abstract`

**Fix:**
```csharp
services.AddChainSharpEffects(o =>
    o.AddEffectWorkflowBus(typeof(YourWorkflow).Assembly)  // Ensure correct assembly
);
```

## "Unable to resolve service for type 'IStep'"

A step's dependency isn't registered in the DI container.

**Cause:** Your step injects a service that wasn't added to `IServiceCollection`.

**Fix:** Register the missing service:
```csharp
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IEmailService, EmailService>();
```

## Step runs but Memory doesn't have the expected type

The chain couldn't find a type in Memory to pass to your step.

**Causes:**
- A previous step didn't return or add the expected type to Memory
- Type mismatch between step output and next step's input

**Fix:** Check the chain flow. Each step's input type must exist in Memory (either from `Activate()` or a previous step's output):
```csharp
Activate(input)                    // Memory: CreateUserRequest
    .Chain<ValidateStep>()         // Takes CreateUserRequest, returns Unit
    .Chain<CreateUserStep>()       // Takes CreateUserRequest, returns User
    .Chain<SendEmailStep>()        // Takes User (from previous step)
    .Resolve();
```

The [Analyzer](../analyzer.md) catches most of these issues at compile time—if you see CHAIN001, the message tells you exactly which type is missing and what's available.

## Workflow completes but metadata shows "Failed"

Check `FailureException` and `FailureReason` in the metadata record for details. Common causes:
- An effect provider failed during `SaveChanges` (database connection, serialization error)
- A step threw after the main workflow logic completed

## Steps execute out of order or skip unexpectedly

If you're using `ShortCircuit`, remember that throwing an exception means "continue" not "stop." See [ShortCircuit](short-circuit.md) for details.
