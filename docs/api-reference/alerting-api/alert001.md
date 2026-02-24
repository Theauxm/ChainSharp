---
layout: default
title: ALERT001
parent: Alerting API
grand_parent: API Reference
---

# ALERT001: AlertConfiguration requires TimeWindow and MinimumFailures

Fires when `AlertConfigurationBuilder.Build()` is called without setting both required fields.

## The Problem

`AlertConfiguration` requires both `TimeWindow` and `MinimumFailures` to be set before `Build()` can be called. Without these, the alerting system doesn't know when to send alerts.

```csharp
// ❌ Error ALERT001
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .Build();  // Missing MinimumFailures()
```

The analyzer catches this at compile time—you'll see the error in your IDE immediately.

## The Fix

Call both `WithinTimeSpan()` and `MinimumFailures()` before `Build()`:

```csharp
// ✅ Correct
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(3)
        .Build();
```

Or use the convenience method:

```csharp
// ✅ Correct - AlertOnEveryFailure() sets both fields
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .AlertOnEveryFailure()
        .Build();
```

## Diagnostic Details

| Property | Value |
|----------|-------|
| **ID** | ALERT001 |
| **Category** | ChainSharp.Alerting |
| **Severity** | Error |
| **Message** | AlertConfiguration.Build() called without setting required fields. Call .WithinTimeSpan() and .MinimumFailures(), or use .AlertOnEveryFailure() before Build(). |

## Examples

### Missing TimeWindow

```csharp
// ❌ Error ALERT001
AlertConfigurationBuilder.Create()
    .MinimumFailures(3)
    .Build();  // Missing WithinTimeSpan()
```

### Missing MinimumFailures

```csharp
// ❌ Error ALERT001
AlertConfigurationBuilder.Create()
    .WithinTimeSpan(TimeSpan.FromHours(1))
    .Build();  // Missing MinimumFailures()
```

### Missing Both

```csharp
// ❌ Error ALERT001
AlertConfigurationBuilder.Create()
    .WhereExceptionType<TimeoutException>()
    .Build();  // Missing both required fields
```

### All Correct

```csharp
// ✅ Both required fields set
AlertConfigurationBuilder.Create()
    .WithinTimeSpan(TimeSpan.FromMinutes(30))
    .MinimumFailures(2)
    .WhereExceptionType<TimeoutException>()
    .Build();

// ✅ Convenience method sets both
AlertConfigurationBuilder.Create()
    .AlertOnEveryFailure()
    .WhereExceptionType<TimeoutException>()
    .Build();
```

## Runtime Fallback

If the analyzer is not active, the runtime validation will throw:

```csharp
InvalidOperationException: TimeWindow must be set. Call WithinTimeSpan() or AlertOnEveryFailure() before Build().
```

```csharp
InvalidOperationException: MinimumFailures must be set. Call MinimumFailures() or AlertOnEveryFailure() before Build().
```

## Suppressing the Diagnostic

If you have a valid reason to suppress this (e.g., dynamic configuration), use a pragma:

```csharp
#pragma warning disable ALERT001
var config = builder.Build();  // Without required fields
#pragma warning restore ALERT001
```

Or suppress at the project level in your `.csproj`:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);ALERT001</NoWarn>
</PropertyGroup>
```

## See Also

- [AlertConfigurationBuilder](alert-configuration-builder.md) - The builder API
- [IAlertingWorkflow](i-alerting-workflow.md) - How to use ConfigureAlerting()
- [ALERT002](alert002.md) - UseAlertingEffect requires alert sender
