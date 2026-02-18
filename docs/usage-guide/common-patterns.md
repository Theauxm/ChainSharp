---
layout: default
title: Common Patterns
parent: Usage Guide
nav_order: 10
---

# Common Patterns

## Error Handling Patterns

### Workflow-Level Error Handling

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
            Logger?.LogWarning("Payment failed for order {OrderId}: {Error}",
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

*API Reference: [Activate]({% link api-reference/workflow-methods/activate.md %}), [Chain]({% link api-reference/workflow-methods/chain.md %}), [Resolve]({% link api-reference/workflow-methods/resolve.md %})*

### Step-Level Error Handling

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
