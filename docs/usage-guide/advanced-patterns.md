---
layout: default
title: Advanced Patterns
parent: Usage Guide
nav_order: 4
---

# Advanced Patterns

## Working with Tuples

ChainSharp's Memory system automatically handles tuples, making it easy to work with multiple types in a single step.

### Returning Multiple Objects

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

### Consuming Multiple Objects

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

## Nested Workflows

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

## Short-Circuiting

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

## Extract

Pull a nested value out of an object in Memory:

```csharp
public class GetUserEmailWorkflow : EffectWorkflow<UserId, string>
{
    protected override async Task<Either<Exception, string>> RunInternal(UserId input)
        => Activate(input)
            .Chain<LoadUserStep>()              // Returns User, stored in Memory
            .Extract<User, EmailAddress>()      // Finds EmailAddress property on User
            .Chain<ValidateEmailStep>()         // Takes EmailAddress from Memory
            .Resolve();
}
```

`Extract<TSource, TTarget>()` looks for a property or field of type `TTarget` on the `TSource` object in Memory. If found, it stores the value in Memory under the `TTarget` type.

## AddServices

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
