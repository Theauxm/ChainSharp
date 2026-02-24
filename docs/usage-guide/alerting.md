---
layout: default
title: Alerting
parent: Usage Guide
nav_order: 11
---

# Alerting

ChainSharp.Effect.Provider.Alerting adds metric-based alerting for workflows. When workflows fail, the system evaluates configurable conditions and sends alerts to your preferred destinations.

## Quick Setup

### Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect.Provider.Alerting
```

### Configuration

```csharp
services.AddChainSharpEffects(options =>
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()
        .AddEffectWorkflowBus(typeof(Program).Assembly)
        .UseAlertingEffect(alertOptions =>
            alertOptions.AddAlertSender<SnsSender>())
);
```

## Implementing IAlertSender

Create a class that defines where alerts are sent:

```csharp
public class SnsSender : IAlertSender
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly ILogger<SnsSender> _logger;

    public SnsSender(
        IAmazonSimpleNotificationService sns,
        ILogger<SnsSender> logger)
    {
        _sns = sns;
        _logger = logger;
    }

    public async Task SendAlertAsync(
        AlertContext context,
        CancellationToken cancellationToken)
    {
        var severity = DetermineSeverity(context);
        var message = FormatMessage(context);
        var topicArn = GetTopicForSeverity(severity);

        await _sns.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Subject = $"[{severity}] {context.WorkflowName}",
            Message = message
        }, cancellationToken);
    }
}
```

## Configuring Workflow Alerts

Implement `IAlertingWorkflow` on your workflow:

```csharp
public class DataSyncWorkflow : EffectWorkflow<SyncInput, Unit>,
    IAlertingWorkflow<SyncInput, Unit>
{
    public AlertConfiguration ConfigureAlerting() =>
        AlertConfigurationBuilder.Create()
            .WithinTimeSpan(TimeSpan.FromHours(1))
            .MinimumFailures(3)
            .Build();

    protected override Task<Either<Exception, Unit>> RunInternal(SyncInput input) =>
        Activate(input)
            .Chain<FetchDataStep>()
            .Chain<SyncDataStep>()
            .Resolve();
}
```

## Alert Configuration Options

### Required Fields

Both `WithinTimeSpan()` and `MinimumFailures()` are required:

```csharp
AlertConfigurationBuilder.Create()
    .WithinTimeSpan(TimeSpan.FromHours(1))  // Look back 1 hour
    .MinimumFailures(3)                     // Alert on 3+ failures
    .Build();
```

### Alert on Every Failure

Convenience method for simple alerting:

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .AlertOnEveryFailure()
        .Build();
```

This skips the database query for maximum performance.

### Exception Type Filters

Alert only on specific exceptions:

```csharp
.WhereExceptionType<TimeoutException>()
.WhereExceptionType<DatabaseException>()
// Alerts if EITHER exception occurs
```

### Step Name Filters

Alert based on which step failed:

```csharp
// Exact match
.WhereFailureStepNameEquals("DatabaseConnectionStep")

// Custom predicate
.WhereFailureStepName(step => step.StartsWith("Database"))
.WhereFailureStepName(step => step.Contains("Connection"))
```

### Custom Filters

Full access to Metadata for complex conditions:

```csharp
.AndCustomFilter(m => m.FailureReason?.Contains("timeout") ?? false)
.AndCustomFilter(m => m.StartTime.Hour >= 9 && m.StartTime.Hour <= 17)
// ALL custom filters must pass
```

## AlertContext Fields

Your `IAlertSender` receives comprehensive context:

| Field | Type | Description |
|-------|------|-------------|
| `WorkflowName` | string | Workflow that triggered alert |
| `TriggerMetadata` | Metadata | The specific failed execution |
| `FailureCount` | int | Failures in time window |
| `TimeWindow` | TimeSpan | Time window evaluated |
| `Configuration` | AlertConfiguration | Rules that triggered |
| `FailedExecutions` | IReadOnlyList<Metadata> | All failed runs |
| `TotalExecutions` | int | Total runs (success + failure) |
| `FirstFailureTime` | DateTime | When failures started |
| `LastSuccessTime` | DateTime? | Last successful run |
| `ExceptionFrequency` | Dictionary<string, int> | Exception counts |
| `FailedStepFrequency` | Dictionary<string, int> | Failed step counts |
| `FailedInputs` | IReadOnlyList<string> | Input JSON from failures |

## Multiple Alert Senders

Register multiple destinations:

```csharp
.UseAlertingEffect(alertOptions =>
    alertOptions
        .AddAlertSender<SnsSender>()
        .AddAlertSender<EmailSender>()
        .AddAlertSender<SlackSender>())
```

All senders receive every alert. If one fails, others still execute.

## Debouncing

Prevent alert spam with cooldown periods:

```csharp
.UseAlertingEffect(alertOptions =>
    alertOptions
        .AddAlertSender<SnsSender>()
        .WithDebouncing(TimeSpan.FromMinutes(15)))
```

After an alert is sent, subsequent alerts for the same workflow are suppressed for 15 minutes.

## Performance

The alerting system is optimized to minimize database load:

**For MinimumFailures == 1:**
- No database query
- Alert sent immediately
- Zero DB overhead

**For MinimumFailures > 1:**
- Single optimized query: `WHERE name = ? AND start_time >= ? AND workflow_state IN (?, ?)`
- All filtering in-memory
- Results ordered and limited

**Debouncing:**
- State stored in IMemoryCache (no DB interaction)
- Per-application instance (not shared across servers)

## Common Patterns

### Alert on Repeated Timeouts

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromMinutes(30))
        .MinimumFailures(2)
        .WhereExceptionType<TimeoutException>()
        .Build();
```

### Alert on Database Step Failures

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(5)
        .WhereFailureStepName(step => step.StartsWith("Database"))
        .Build();
```

### Alert During Business Hours Only

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(3)
        .AndCustomFilter(m =>
        {
            var hour = m.StartTime.Hour;
            return hour >= 9 && hour <= 17;
        })
        .Build();
```

### Complex Routing in Alert Sender

```csharp
public class SmartSender : IAlertSender
{
    public async Task SendAlertAsync(AlertContext context, CancellationToken ct)
    {
        // Determine severity from failure rate and exception types
        var failureRate = (double)context.FailureCount / context.TotalExecutions;
        var hasCriticalException = context.ExceptionFrequency
            .ContainsKey("DatabaseException");
        
        var severity = (failureRate > 0.5 || hasCriticalException) 
            ? "CRITICAL" 
            : "WARNING";
        
        // Route to different channels based on severity
        if (severity == "CRITICAL")
        {
            await SendToPagerDuty(context, ct);
            await SendToSlack(context, "#alerts-critical", ct);
        }
        else
        {
            await SendToSlack(context, "#alerts-standard", ct);
        }
    }
}
```

## Troubleshooting

### Alerts Not Sending

**Check workflow implements IAlertingWorkflow:**
```csharp
// ✅ Correct
public class MyWorkflow : EffectWorkflow<Input, Output>,
    IAlertingWorkflow<Input, Output>

// ❌ Missing interface
public class MyWorkflow : EffectWorkflow<Input, Output>
```

**Check logs for condition evaluation:**
```
DEBUG: Evaluating alert conditions for workflow MyNamespace.MyWorkflow
DEBUG: Alert conditions not met: 2 failures < 3 required
```

**Verify alert sender is registered:**
```csharp
.UseAlertingEffect(alertOptions =>
    alertOptions.AddAlertSender<MySender>())  // Must call this
```

### "Could not register alert configuration" Warning

Your workflow has constructor dependencies. Options:

**Add parameterless constructor:**
```csharp
public class MyWorkflow : EffectWorkflow<Input, Output>,
    IAlertingWorkflow<Input, Output>
{
    // Public parameterless for scanning
    public MyWorkflow() { }
    
    // Private constructor with dependencies (used at runtime)
    [Inject]
    public MyWorkflow(IMyService service) { }
}
```

**Use property injection:**
```csharp
public class MyWorkflow : EffectWorkflow<Input, Output>,
    IAlertingWorkflow<Input, Output>
{
    [Inject]
    public IMyService? MyService { get; set; }
}
```

### Database Queries Too Slow

**Ensure proper indexing:**
```sql
CREATE INDEX idx_metadata_alert_lookup 
ON metadata (name, start_time DESC, workflow_state);
```

**Reduce time window:**
```csharp
.WithinTimeSpan(TimeSpan.FromMinutes(30))  // Instead of hours
```

**Use MinimumFailures == 1:**
```csharp
.AlertOnEveryFailure()  // Skips DB entirely
```

## See Also

- [Effect Providers](effect-providers.md) - How effects work in ChainSharp
- [Metadata](../concepts/metadata.md) - Understanding workflow metadata
- [Mediator](mediator.md) - Dispatching workflows with WorkflowBus
