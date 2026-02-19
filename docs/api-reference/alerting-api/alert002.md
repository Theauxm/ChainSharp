---
layout: default
title: ALERT002
parent: Alerting API
grand_parent: API Reference
---

# ALERT002: UseAlertingEffect requires at least one alert sender

Fires when `UseAlertingEffect()` is called without registering any `IAlertSender` implementations.

## The Problem

The alerting effect needs at least one destination to send alerts to. Without an `IAlertSender`, alerts would be evaluated but never delivered.

```csharp
// ❌ Error ALERT002
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
    {
        // Nothing here - no AddAlertSender() call
    })
);
```

The analyzer catches this at compile time—you'll see the error in your IDE immediately.

## The Fix

Call `AddAlertSender<T>()` at least once inside the configure action:

```csharp
// ✅ Correct
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
        alertOptions.AddAlertSender<SnsSender>())
);
```

## Diagnostic Details

| Property | Value |
|----------|-------|
| **ID** | ALERT002 |
| **Category** | ChainSharp.Alerting |
| **Severity** | Error |
| **Message** | UseAlertingEffect() called without adding any alert senders. Call options.AddAlertSender<TYourSender>() inside the configure action. |

## Examples

### Single Alert Sender

```csharp
// ✅ Correct
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
        alertOptions.AddAlertSender<SnsSender>())
);
```

### Multiple Alert Senders

```csharp
// ✅ Correct - multiple senders
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
        alertOptions
            .AddAlertSender<SnsSender>()
            .AddAlertSender<EmailSender>()
            .AddAlertSender<SlackSender>())
);
```

### With Debouncing

```csharp
// ✅ Correct - sender + debouncing
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
        alertOptions
            .AddAlertSender<SnsSender>()
            .WithDebouncing(TimeSpan.FromMinutes(15)))
);
```

### Incorrect - Empty Lambda

```csharp
// ❌ Error ALERT002
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions => { })
);

// ❌ Error ALERT002  
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(_ => { })
);
```

### Incorrect - Only Debouncing

```csharp
// ❌ Error ALERT002 - has debouncing but no sender
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
        alertOptions.WithDebouncing(TimeSpan.FromMinutes(15)))
);
```

## Runtime Fallback

If the analyzer is not active, the runtime validation will throw:

```csharp
InvalidOperationException: At least one alert sender must be registered. 
Call options.AddAlertSender<TYourSender>() in the configure action.
```

## Suppressing the Diagnostic

If you have a valid reason to suppress this, use a pragma:

```csharp
#pragma warning disable ALERT002
options.UseAlertingEffect(alertOptions => { });
#pragma warning restore ALERT002
```

Or suppress at the project level in your `.csproj`:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);ALERT002</NoWarn>
</PropertyGroup>
```

## See Also

- [UseAlertingEffect](use-alerting-effect.md) - Service registration method
- [AlertingOptionsBuilder](alerting-options-builder.md) - Options builder API
- [IAlertSender](i-alert-sender.md) - How to implement alert senders
- [ALERT001](alert001.md) - AlertConfiguration required fields
