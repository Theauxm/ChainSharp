---
layout: default
title: Usage Guide
nav_order: 5
---

# Usage Guide

This guide provides step-by-step examples for implementing workflows using ChainSharp, covering common scenarios from basic setups to advanced patterns.

## Basic Workflow Example

A workflow in ChainSharp represents a sequence of steps that process data in a linear fashion:

```csharp
using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using LanguageExt;

// Define a workflow that takes Ingredients as input and produces a List<GlassBottle> as output
public class Cider
    : Workflow<Ingredients, List<GlassBottle>>,
        ICider
{
    // Implement the RunInternal method to define the workflow steps
    protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
        Ingredients input
    ) => Activate(input) 
            .Chain<Prepare>() // Chain steps together
            .Chain<Ferment>()
            .Chain<Brew>()
            .Chain<Bottle>()
            .Resolve(); // Resolve the final result
}
```

## Step Anatomy

Steps are the building blocks of workflows. Each step performs a specific operation:

```csharp
using ChainSharp.Exceptions;
using ChainSharp.Step;
using LanguageExt;

// Define a step that takes Ingredients as input and produces a BrewingJug as output
public class Prepare : Step<Ingredients, BrewingJug>, IPrepare
{
    // Implement the Run method to define the step's operation
    public override async Task<BrewingJug> Run(Ingredients input)
    {
        const int gallonWater = 1;

        // Perform the step's operation
        var gallonAppleJuice = Boil(gallonWater, input.Apples, input.BrownSugar);

        // Handle errors
        if (gallonAppleJuice < 0)
            throw new Exception("Couldn't make a Gallon of Apple Juice!");

        // Return the result
        return new BrewingJug() { Gallons = gallonAppleJuice, Ingredients = input };
    }

    // Helper method for the step
    private int Boil(int gallonWater, int numApples, int ozBrownSugar) 
        => gallonWater + (numApples / 8) + (ozBrownSugar / 128);
}
```

## EffectWorkflow Example

EffectWorkflows extend the basic workflow concept with effects for logging, data persistence, and more:

```csharp
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

// Define an EffectWorkflow
public class ExampleEffectWorkflow
    : EffectWorkflow<WorkflowInput, WorkflowOutput>,
        IExampleEffectWorkflow
{
    protected override async Task<Either<Exception, WorkflowOutput>> RunInternal(
        WorkflowInput input
    ) => Activate(input)
        .Chain<StepOne>()
        .Chain<StepTwo>()
        .Chain<StepThree>()
        .Resolve();
}

// Define the interface for the workflow
public interface IExampleEffectWorkflow
    : IEffectWorkflow<WorkflowInput, WorkflowOutput> { }
```

## Common Patterns

### Error Handling Patterns

#### Workflow-Level Error Handling

```csharp
public class RobustWorkflow : EffectWorkflow<ProcessOrderRequest, ProcessOrderResult>
{
    protected override async Task<Either<Exception, ProcessOrderResult>> RunInternal(ProcessOrderRequest input)
    {
        try
        {
            return await Activate(input)
                .Chain<ValidateOrderStep>()
                .Chain<ProcessPaymentStep>()
                .Chain<FulfillOrderStep>()
                .Resolve();
        }
        catch (PaymentException ex)
        {
            // Handle payment-specific errors
            EffectLogger?.LogWarning("Payment failed for order {OrderId}: {Error}", 
                input.OrderId, ex.Message);
            return new OrderProcessingException("Payment processing failed", ex);
        }
        catch (InventoryException ex)
        {
            // Handle inventory-specific errors
            return new OrderProcessingException("Insufficient inventory", ex);
        }
    }
}
```

#### Step-Level Error Handling

```csharp
public class RobustStep(IPaymentGateway PaymentGateway) : Step<PaymentRequest, PaymentResult>
{
    public override async Task<PaymentResult> Run(PaymentRequest input)
    {
        try
        {
            var result = await PaymentGateway.ProcessAsync(input);
            return result;
        }
        catch (TimeoutException ex)
        {
            // Throw a meaningful error
            throw new PaymentException("Payment gateway timed out", ex);
        }
    }
}
```

## WorkflowBus Usage

The WorkflowBus provides a way to run workflows from anywhere in your application.

### Using WorkflowBus in a Step

```csharp
using ChainSharp.Step;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using LanguageExt;

// Define a step that runs another workflow
internal class StepToRunNestedWorkflow(IWorkflowBus workflowBus) : Step<Unit, IInternalWorkflow>
{
    public override async Task<IInternalWorkflow> Run(Unit input)
    {
        // Run a workflow using the WorkflowBus
        var testWorkflow = await workflowBus.RunAsync<IInternalWorkflow>(input);
        return testWorkflow;
    }
}
```

### Using WorkflowBus in a Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Exceptions;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IWorkflowBus _workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        try
        {
            var order = await _workflowBus.RunAsync<Order>(request);
            return Ok(order);
        }
        catch (WorkflowException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        try
        {
            var order = await _workflowBus.RunAsync<Order>(new GetOrderRequest { Id = id });
            return Ok(order);
        }
        catch (WorkflowException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (WorkflowException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
```

### Registering WorkflowBus with Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using ChainSharp.Effect.Extensions;

// Register ChainSharp services with dependency injection
new ServiceCollection()
    .AddChainSharpEffects(o => o.AddEffectWorkflowBus(assemblies: [typeof(AssemblyMarker).Assembly]));
```

## Advanced Patterns

### Working with Tuples

ChainSharp's Memory system automatically handles tuples, making it easy to work with multiple types in a single step.

#### Returning Multiple Objects

When a step needs to produce multiple objects, return them as a tuple. Memory will automatically deconstruct and store each type individually:

```csharp
public class LoadEntitiesStep(IRepository Repository) : Step<LoadRequest, (User, Order, Payment)>
{
    public override async Task<(User, Order, Payment)> Run(LoadRequest input)
    {
        // Load multiple entities
        var user = await Repository.GetUserAsync(input.UserId);
        var order = await Repository.GetOrderAsync(input.OrderId);
        var payment = await Repository.GetPaymentAsync(input.PaymentId);
        
        // Return as tuple - each will be stored individually in Memory
        return (user, order, payment);
    }
}
```

#### Consuming Multiple Objects

When a step needs multiple objects from Memory, declare them as a tuple input. Memory will find each type and construct the tuple automatically:

```csharp
public class ProcessCheckoutStep(ICheckoutService CheckoutService) : Step<(User, Order, Payment), Receipt>
{
    public override async Task<Receipt> Run((User User, Order Order, Payment Payment) input)
    {
        // All three objects were found individually in Memory and combined
        return await CheckoutService.ProcessAsync(input.User, input.Order, input.Payment);
    }
}

// Complete workflow example
public class CheckoutWorkflow : EffectWorkflow<CheckoutRequest, Receipt>
{
    protected override async Task<Either<Exception, Receipt>> RunInternal(CheckoutRequest input)
        => Activate(input)
            .Chain<LoadEntitiesStep>()     // Returns (User, Order, Payment) - deconstructed
            .Chain<ValidateUserStep>()     // Takes User from Memory
            .Chain<ValidateOrderStep>()    // Takes Order from Memory  
            .Chain<ProcessCheckoutStep>()  // Takes (User, Order, Payment) - reconstructed
            .Resolve();
}
```

### Nested Workflows

```csharp
public class ParentWorkflow(IWorkflowBus WorkflowBus) : EffectWorkflow<ParentRequest, ParentResult>
{
    protected override async Task<Either<Exception, ParentResult>> RunInternal(ParentRequest input)
    {
        // Run child workflow with parent metadata for tracking
        var childResult = await WorkflowBus.RunAsync<ChildResult>(
            new ChildRequest { Data = input.ChildData },
            Metadata  // Establishes parent-child relationship
        );
        
        return new ParentResult
        {
            ParentData = input.ParentData,
            ChildResult = childResult
        };
    }
}
```
