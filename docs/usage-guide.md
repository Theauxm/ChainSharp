---
layout: default
title: Usage Guide
nav_order: 4
---

# Usage Guide

Practical examples and patterns for building workflows.

## Configuring Effect Providers

ChainSharp has several effect providers. Here's when to use each:

### Database Persistence (Postgres or InMemory)

**Use when:** You need to query workflow history, audit execution, or debug production issues.

```csharp
// Production
services.AddChainSharpEffects(options => 
    options.AddPostgresEffect("Host=localhost;Database=app;Username=postgres;Password=pass")
);

// Testing
services.AddChainSharpEffects(options => 
    options.AddInMemoryEffect()
);
```

This persists a `Metadata` record for each workflow execution containing:
- Workflow name and state (Pending → InProgress → Completed/Failed)
- Start and end timestamps
- Serialized input and output
- Exception details if failed
- Parent workflow ID for nested workflows

### JSON Effect (`AddJsonEffect`)

**Use when:** Debugging during development. Logs workflow state changes to your configured logger.

```csharp
services.AddChainSharpEffects(options => 
    options.AddJsonEffect()
);
```

This doesn't persist anything—it just logs. Useful for seeing what's happening without setting up a database.

### Parameter Effect (`SaveWorkflowParameters`)

**Use when:** You need to store workflow inputs/outputs in the database for later querying or replay.

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()  // Serializes Input/Output to Metadata
);
```

Without this, the `Input` and `Output` columns in `Metadata` are null. With it, they contain JSON-serialized versions of your request and response objects.

### Combining Providers

Providers compose. A typical production setup:

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)   // Persist metadata
        .SaveWorkflowParameters()              // Include input/output in metadata
        .AddEffectWorkflowBus(assemblies)      // Enable workflow discovery
);
```

A typical development setup:

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddInMemoryEffect()                   // Fast, no database needed
        .AddJsonEffect()                       // Log state changes
        .AddEffectWorkflowBus(assemblies)
);
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

## Step Patterns

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

### AddServices

You can add service instances to Memory and later use them in steps:

```csharp
protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
{
    var validator = new CustomValidator();
    var notifier = new SlackNotifier();
    
    return Activate(input)
        .AddServices<IValidator, INotifier>(validator, notifier)
        .Chain<CreateUserStep>()
        .Resolve();
}
```

## Advanced Patterns

### Error Handling

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

> ⚠️ **This behavior is intentionally inverted from `Chain`.** A `Chain` step that throws stops the workflow with an error. A `ShortCircuit` step that throws means "no short-circuit available, keep going." This can be surprising if you're not expecting it.

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

### Testing with AddServices and IChain

You can use `AddServices` to inject fake step implementations and `IChain` to run them by interface:

```csharp
public class FakeEmailService : IEmailService
{
    public List<string> SentEmails { get; } = [];
    
    public Task SendWelcomeEmailAsync(string email, string name)
    {
        SentEmails.Add(email);
        return Task.CompletedTask;
    }
}

[Test]
public async Task Workflow_UsesFakeStep()
{
    var fakeEmail = new FakeEmailService();
    var workflow = new TestWorkflow(fakeEmail);
    
    var result = await workflow.Run(new CreateUserRequest { Email = "test@example.com" });
    
    Assert.True(result.IsRight);
    Assert.Contains("test@example.com", fakeEmail.SentEmails);
}

public class TestWorkflow(IEmailService emailService) : Workflow<CreateUserRequest, User>
{
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .AddServices(emailService)
            .Chain<CreateUserStep>()
            .IChain<IEmailService>()  // Runs the fake
            .Resolve();
}
```

## Troubleshooting

### Don't use `[Inject]` for dependency injection

The `[Inject]` attribute is used **internally** by the `EffectWorkflow` base class for its own framework-level services (`IEffectRunner`, `ILogger`, `IServiceProvider`). 

You do NOT need to use `[Inject]` anywhere in your workflow or step code. Steps should use standard constructor injection:

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
        var result = await MyService.DoSomethingAsync(input);
        return result;
    }
}
```

### Steps don't need to "pass through" their input type

You do NOT need to return the same type you receive as input. The workflow's **Memory** system stores references to all objects, and modifications are automatically reflected.

```csharp
// ❌ WRONG - Unnecessarily passing through the User type
public class UpdateUserStep : Step<User, User>
{
    public override async Task<User> Run(User input)
    {
        input.LastModified = DateTime.UtcNow;
        return input;  // Unnecessarily returning the same object
    }
}

// ✅ CORRECT - The reference in Memory is already updated
public class UpdateUserStep : Step<User, Unit>
{
    public override async Task<Unit> Run(User input)
    {
        input.LastModified = DateTime.UtcNow;
        return Unit.Default;  // No need to return User
    }
}
```

**Why does this work?** ChainSharp's Memory stores objects by their type. When you modify an object (like `User`), you're modifying the same reference that's stored in Memory. Subsequent steps that need the `User` will automatically get the updated object.

**Rule of thumb:** Only return a type from a step if it's a NEW type being added to Memory, not the same type you received.

### "No workflow found for input type X"

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

### "Unable to resolve service for type 'IStep'"

A step's dependency isn't registered in the DI container.

**Cause:** Your step injects a service that wasn't added to `IServiceCollection`.

**Fix:** Register the missing service:
```csharp
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IEmailService, EmailService>();
```

### Step runs but Memory doesn't have the expected type

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

### Workflow completes but metadata shows "Failed"

Check `FailureException` and `FailureReason` in the metadata record for details. Common causes:
- An effect provider failed during `SaveChanges` (database connection, serialization error)
- A step threw after the main workflow logic completed

### Steps execute out of order or skip unexpectedly

If you're using `ShortCircuit`, remember that throwing an exception means "continue" not "stop." See [Short-Circuiting](#short-circuiting) for details.
