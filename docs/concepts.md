---
layout: default
title: Core Concepts
nav_order: 3
---

# Core Concepts

## Railway Oriented Programming

Railway Oriented Programming comes from functional programming. The idea: your code has two tracks, success and failure. Each operation either continues down the success track or switches to the failure track.

```
Success Track:  Input → [Step 1] → [Step 2] → [Step 3] → Output
                            ↓
Failure Track:          Exception → [Skip] → [Skip] → Exception
```

ChainSharp uses `Either<Exception, T>` from LanguageExt to represent this. A value is either `Left` (an exception) or `Right` (the success value):

```csharp
public async Task<Either<Exception, PaymentReceipt>> ProcessPayment(PaymentRequest input)
{
    return Activate(input)
        .Chain<ValidateCardStep>()      // If this fails, skip remaining steps
        .Chain<CheckFraudStep>()        // Only runs if card is valid
        .Chain<ChargeCardStep>()        // Only runs if fraud check passed
        .Resolve();                     // Return Either<Exception, PaymentReceipt>
}
```

If `ValidateCardStep` throws, the workflow immediately returns `Left(exception)`. `CheckFraudStep` and `ChargeCardStep` never execute. You don't write any error-checking code—the chain handles it.

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

A **workflow** is a sequence of steps that accomplish a business operation. `ProcessPaymentWorkflow` chains together card validation, fraud checking, and card charging.

A **step** does one thing. `ValidateCardStep` checks if a card number is valid. `ChargeCardStep` processes the actual payment. Steps are easy to test in isolation because they have a single responsibility.

**Effects** are cross-cutting concerns that happen as a result of steps running—database writes, log entries, serialized parameters. Effect providers collect these during workflow execution and apply them atomically at the end.
