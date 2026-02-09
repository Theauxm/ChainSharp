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

### Using WorkflowBus in a REST Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Exceptions;

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
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        try
        {
            var order = await workflowBus.RunAsync<Order>(new GetOrderRequest { Id = id });
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

### Injecting Workflows Directly

You don't have to use `IWorkflowBus`. When you use `AddEffectWorkflowBus`, all discovered workflows are also registered in DI by their interface. You can inject them directly:

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

This is useful when:
- You know exactly which workflow you need at compile time
- You want clearer type signatures in your resolvers/handlers
- You prefer explicit dependencies over the mediator pattern

The workflow still gets all the same effect handling (metadata, persistence, etc.)—it's just resolved differently.

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

### Short-Circuiting

Sometimes a step should end the workflow early with a valid result (not an error). Use `ShortCircuit<TStep>()` for this:

```csharp
public class ProcessOrderWorkflow : EffectWorkflow<OrderRequest, OrderResult>
{
    protected override async Task<Either<Exception, OrderResult>> RunInternal(OrderRequest input)
        => Activate(input)
            .Chain<ValidateOrderStep>()
            .ShortCircuit<CheckCacheStep>()  // If cached, return early
            .Chain<CalculatePricingStep>()   // Skipped if cache hit
            .Chain<ProcessPaymentStep>()     // Skipped if cache hit
            .Chain<SaveOrderStep>()
            .Resolve();
}

public class CheckCacheStep(ICache Cache) : Step<OrderRequest, OrderResult>
{
    public override async Task<OrderResult> Run(OrderRequest input)
    {
        var cached = await Cache.GetAsync<OrderResult>(input.OrderId);
        
        if (cached != null)
            return cached;  // This becomes the workflow's final result
        
        // Throwing signals "no short-circuit, continue the chain"
        throw new Exception("Cache miss");
    }
}
```

When a `ShortCircuit` step returns a value of the workflow's return type (`OrderResult` here), that value becomes the final result and remaining steps are skipped. If the step throws, the workflow continues normally (the exception is swallowed, not propagated).

This is different from regular `Chain`—a `Chain` step that throws stops the workflow with an error. A `ShortCircuit` step that throws just means "keep going."

### Extract

Pull a nested value out of an object in Memory:

```csharp
public class GetUserEmailWorkflow : EffectWorkflow<UserId, string>
{
    protected override async Task<Either<Exception, string>> RunInternal(UserId input)
        => Activate(input)
            .Chain<LoadUserStep>()              // Returns User, stored in Memory
            .Extract<User, EmailAddress>()      // Finds EmailAddress property on User
            .Chain<ValidateEmailStep>()         // Takes EmailAddress from Memory
            .Resolve<string>();
}
```

`Extract<TSource, TTarget>()` looks for a property or field of type `TTarget` on the `TSource` object in Memory. If found, it stores the value in Memory under the `TTarget` type.

### AddServices and IChain

You can add service instances to Memory and later chain them by interface:

```csharp
protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
{
    var ferment = new Ferment();
    var prepare = new Prepare(ferment);
    
    return Activate(input)
        .AddServices<IPrepare, IFerment>(prepare, ferment)  // Store in Memory by interface
        .IChain<IPrepare>()    // Find IPrepare in Memory and execute it
        .IChain<IFerment>()    // Find IFerment in Memory and execute it
        .Chain<Bottle>()
        .Resolve();
}
```

`IChain<TInterface>()` requires the type parameter to be an interface. It finds the implementation in Memory (added via `AddServices`) and executes it. This is useful when you need to control which implementation runs, or when testing with fakes.

## Testing

### Unit Testing Steps

Steps are easy to test because they're just classes with a `Run` method. Create simple fake implementations of your dependencies:

```csharp
// A simple fake repository for testing
public class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];
    private int _nextId = 1;
    
    public Task<User?> GetByEmailAsync(string email) 
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
    
    public Task<User> CreateAsync(User user)
    {
        user = user with { Id = _nextId++ };
        _users.Add(user);
        return Task.FromResult(user);
    }
    
    // Seed data for tests
    public void AddExisting(User user) => _users.Add(user);
}

[Test]
public async Task ValidateEmailStep_ThrowsForDuplicateEmail()
{
    // Arrange
    var repo = new FakeUserRepository();
    repo.AddExisting(new User { Id = 1, Email = "taken@example.com" });
    
    var step = new ValidateEmailStep(repo);
    var request = new CreateUserRequest { Email = "taken@example.com" };
    
    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(() => step.Run(request));
}

[Test]
public async Task CreateUserStep_ReturnsNewUser()
{
    // Arrange
    var repo = new FakeUserRepository();
    var step = new CreateUserStep(repo);
    var request = new CreateUserRequest 
    { 
        Email = "new@example.com",
        FirstName = "Test",
        LastName = "User"
    };
    
    // Act
    var result = await step.Run(request);
    
    // Assert
    Assert.Equal(1, result.Id);  // First user gets ID 1
    Assert.Equal("new@example.com", result.Email);
}
```

### Unit Testing Workflows

Register your fakes in the service collection:

```csharp
[Test]
public async Task CreateUserWorkflow_CreatesUser()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IUserRepository, FakeUserRepository>();
    services.AddSingleton<IEmailService, FakeEmailService>();
    services.AddChainSharpEffects(o => o.AddEffectWorkflowBus(typeof(CreateUserWorkflow).Assembly));
    
    var provider = services.BuildServiceProvider();
    var bus = provider.GetRequiredService<IWorkflowBus>();
    
    // Act
    var result = await bus.RunAsync<User>(new CreateUserRequest 
    { 
        Email = "test@example.com",
        FirstName = "Test",
        LastName = "User"
    });
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal("test@example.com", result.Email);
}
```

### Integration Testing with InMemory Provider

For integration tests, use the InMemory data provider to avoid database dependencies:

```csharp
[Test]
public async Task Workflow_PersistsMetadata()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddSingleton<IUserRepository, FakeUserRepository>();
    services.AddChainSharpEffects(options => 
        options
            .AddInMemoryEffect()
            .AddEffectWorkflowBus(typeof(CreateUserWorkflow).Assembly)
    );
    
    var provider = services.BuildServiceProvider();
    var bus = provider.GetRequiredService<IWorkflowBus>();
    var context = provider.GetRequiredService<IDataContext>();
    
    // Act
    await bus.RunAsync<User>(new CreateUserRequest { Email = "test@example.com" });
    
    // Assert
    var metadata = await context.Metadatas.FirstOrDefaultAsync();
    Assert.NotNull(metadata);
    Assert.Equal(WorkflowState.Completed, metadata.WorkflowState);
}
```

### Testing with Fake Steps

You can use `AddServices` to inject fake step implementations:

```csharp
// A fake step that returns a predictable result
public class FakeFerment : IFerment
{
    public Task<BrewingJug> Run(BrewingJug input) 
        => Task.FromResult(new BrewingJug { Gallons = 100 });
}

[Test]
public async Task Workflow_UsesFakeStep()
{
    var fakeFerment = new FakeFerment();
    
    var workflow = new TestWorkflow(fakeFerment);
    var result = await workflow.Run(new Ingredients());
    
    Assert.True(result.IsRight);
    Assert.Equal(100, result.Match(_ => 0, jug => jug.Gallons));
}

public class TestWorkflow(IFerment ferment) : Workflow<Ingredients, BrewingJug>
{
    protected override async Task<Either<Exception, BrewingJug>> RunInternal(Ingredients input)
        => Activate(input)
            .AddServices(ferment)
            .Chain<Prepare>()
            .IChain<IFerment>()
            .Resolve();
}
```
