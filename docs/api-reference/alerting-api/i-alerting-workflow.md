---
layout: default
title: IAlertingWorkflow
parent: Alerting API
grand_parent: API Reference
---

# IAlertingWorkflow&lt;TIn, TOut&gt;

Interface that workflows implement to enable alert notifications on failure.

## Signature

```csharp
public interface IAlertingWorkflow<TIn, TOut> : IEffectWorkflow<TIn, TOut>
```

**Type Parameters:**
- `TIn` - The input type for the workflow
- `TOut` - The output type for the workflow

## Methods

### ConfigureAlerting()

Configures the conditions under which this workflow should send alerts.

```csharp
AlertConfiguration ConfigureAlerting()
```

**Returns:** An `AlertConfiguration` defining when alerts should be sent

**Remarks:**

**IMPORTANT:** This method is called once during application startup and the result is cached. It should not depend on runtime state or perform expensive operations.

The returned configuration must satisfy:
- `TimeWindow` must be set (use `WithinTimeSpan()` or `AlertOnEveryFailure()`)
- `MinimumFailures` must be set (use `MinimumFailures()` or `AlertOnEveryFailure()`)

The analyzer (ALERT001) enforces these requirements at compile time.

## Implementation Example

```csharp
public class DataSyncWorkflow : EffectWorkflow<SyncInput, Unit>,
    IAlertingWorkflow<SyncInput, Unit>
{
    public AlertConfiguration ConfigureAlerting() =>
        AlertConfigurationBuilder.Create()
            .WithinTimeSpan(TimeSpan.FromHours(1))
            .MinimumFailures(3)
            .WhereExceptionType<TimeoutException>()
            .Build();

    protected override Task<Either<Exception, Unit>> RunInternal(SyncInput input) =>
        Activate(input)
            .Chain<FetchDataStep>()
            .Chain<ProcessDataStep>()
            .Resolve();
}
```

## Common Patterns

### Alert on Every Failure

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .AlertOnEveryFailure()
        .Build();
```

### Alert on Repeated Timeouts

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromMinutes(30))
        .MinimumFailures(2)
        .WhereExceptionType<TimeoutException>()
        .Build();
```

### Alert on Database Failures During Business Hours

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(5)
        .WhereFailureStepName(step => step.StartsWith("Database"))
        .AndCustomFilter(m =>
        {
            var hour = m.StartTime.Hour;
            return hour >= 9 && hour <= 17;  // Business hours only
        })
        .Build();
```

## How It Works

When a workflow implementing `IAlertingWorkflow` fails:

1. The `OnError` hook triggers in the `AlertingEffect` provider
2. The cached alert configuration is retrieved from `IAlertConfigurationRegistry`
3. If `MinimumFailures == 1`, alert is sent immediately (no DB query)
4. If `MinimumFailures > 1`, a single DB query retrieves historical failures
5. Filters are applied in-memory to the query results
6. If conditions are met, all registered `IAlertSender` implementations are called
7. Debounce cooldown is set (if enabled)

## Troubleshooting

### "Could not register alert configuration" Warning

Your workflow has constructor dependencies. The alerting system requires a parameterless constructor to call `ConfigureAlerting()` during startup.

**Solution 1 - Add Parameterless Constructor:**
```csharp
public class MyWorkflow : EffectWorkflow<Input, Output>,
    IAlertingWorkflow<Input, Output>
{
    // Public parameterless for alerting registry scanning
    public MyWorkflow() { }
    
    // Constructor with dependencies (used at runtime via DI)
    public MyWorkflow(IMyService service, ILogger<MyWorkflow> logger)
    {
        // ...
    }
    
    public AlertConfiguration ConfigureAlerting() =>
        AlertConfigurationBuilder.Create()
            .AlertOnEveryFailure()
            .Build();
}
```

**Solution 2 - Use Property Injection:**
```csharp
public class MyWorkflow : EffectWorkflow<Input, Output>,
    IAlertingWorkflow<Input, Output>
{
    [Inject]
    public IMyService? MyService { get; set; }
    
    [Inject]
    public ILogger<MyWorkflow>? Logger { get; set; }
    
    public AlertConfiguration ConfigureAlerting() =>
        AlertConfigurationBuilder.Create()
            .AlertOnEveryFailure()
            .Build();
}
```

## See Also

- [AlertConfigurationBuilder](alert-configuration-builder.md) - Building alert configurations
- [AlertConfiguration](alert-configuration.md) - The returned configuration object
- [Usage Guide: Alerting](../../usage-guide/alerting.md)
- [ALERT001](alert001.md) - Analyzer for configuration validation
