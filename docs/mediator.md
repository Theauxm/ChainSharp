---
layout: default
title: Mediator
nav_order: 5
---

# Mediator & WorkflowBus

The `ChainSharp.Effect.Orchestration.Mediator` package provides the `WorkflowBus`—a way to dispatch workflows by their input type instead of injecting each one directly.

## The Mediator Pattern

Normally, if a controller needs to run a workflow, it injects the workflow directly:

```csharp
public class UsersController(ICreateUserWorkflow createUserWorkflow) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var user = await createUserWorkflow.Run(request);
        return Ok(user);
    }
}
```

This works, but the controller needs to know about every workflow it calls. The mediator pattern replaces those direct dependencies with a single dispatch point:

```csharp
public class UsersController(IWorkflowBus workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var user = await workflowBus.RunAsync<User>(request);
        return Ok(user);
    }
}
```

The `WorkflowBus` looks at the input type (`CreateUserRequest`), finds the workflow registered for that type, and runs it. The controller doesn't need to know which workflow class handles the request—it just sends the input and gets back a result.

## Setup

```bash
dotnet add package Theauxm.ChainSharp.Effect.Orchestration.Mediator
```

Register workflows during startup by pointing the bus at your assemblies:

```csharp
builder.Services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(typeof(Program).Assembly)
);
```

`AddEffectWorkflowBus` scans the given assemblies for all classes implementing `IEffectWorkflow<TIn, TOut>`, builds a mapping from input types to workflow types, and registers everything in DI. Pass multiple assemblies if your workflows live in different projects.

## Input Type Uniqueness

Each input type maps to exactly one workflow. This is enforced at startup—if two workflows accept the same input type, registration fails:

```csharp
// ❌ This causes a startup error — both accept UserRequest
public class CreateUserWorkflow : EffectWorkflow<UserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UserRequest, User> { }

// ✅ Use distinct input types
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UpdateUserRequest, User> { }
```

This constraint is what makes the mediator pattern work—there's no ambiguity about which workflow handles a given input.

## Using WorkflowBus in a Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(IWorkflowBus workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        try
        {
            var order = await workflowBus.RunAsync<Order>(request);
            return Ok(order);
        }
        catch (WorkflowException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
```

## Nested Workflows

Steps and workflows can run other workflows through the `WorkflowBus`. Pass the current `Metadata` to establish a parent-child relationship:

```csharp
public class ParentWorkflow(IWorkflowBus WorkflowBus) : EffectWorkflow<ParentRequest, ParentResult>
{
    protected override async Task<Either<Exception, ParentResult>> RunInternal(ParentRequest input)
    {
        var childResult = await WorkflowBus.RunAsync<ChildResult>(
            new ChildRequest { Data = input.ChildData },
            Metadata  // Links child metadata to this workflow's metadata
        );

        return new ParentResult
        {
            ParentData = input.ParentData,
            ChildResult = childResult
        };
    }
}
```

The parent's `Metadata.Id` becomes the child's `ParentId`, creating a tree you can query to trace execution across workflows.

## Direct Injection

You don't have to use the bus. `AddEffectWorkflowBus` also registers each workflow by its interface, so you can inject them directly:

```csharp
// GraphQL mutation using Hot Chocolate
[ExtendObjectType("Mutation")]
public class UpdateUserMutation : IGqlMutation
{
    public async Task<UpdateUserOutput> UpdateUser(
        [Service] IUpdateUserWorkflow updateUserWorkflow,
        UpdateUserInput input
    ) => await updateUserWorkflow.Run(input);
}
```

Use direct injection when:
- You know exactly which workflow you need at compile time
- You want stronger type signatures
- You prefer explicit dependencies over the mediator pattern

The workflow still gets all the same effect handling (metadata, persistence, etc.)—it's just resolved differently.
