---
layout: default
title: ShortCircuit
parent: Usage Guide
nav_order: 5
---

# ShortCircuit

`.ShortCircuit<TStep>()` lets a step end the workflow early with a valid result. If the step returns a value of the workflow's return type, that becomes the final result and remaining steps are skipped. If the step throws, the workflow continues normally.

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
```

## The Step

A `ShortCircuit` step returns the result when it wants to short-circuit, and throws when it doesn't:

```csharp
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

> **This behavior is intentionally inverted from Chain.** A `Chain` step that throws stops the workflow with an error. A `ShortCircuit` step that throws means "no short-circuit available, keep going." The exception is swallowed, not propagated.

## When to Use It

- **Caching** — return a cached result if available, otherwise compute it
- **Feature flags** — return a default result if a feature is disabled
- **Early exits** — skip expensive processing when a precondition is already satisfied
