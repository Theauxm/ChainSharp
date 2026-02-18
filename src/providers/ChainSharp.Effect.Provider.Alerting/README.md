# ChainSharp.Effect.Provider.Alerting

Metric-based alerting for ChainSharp workflows with customizable conditions and multiple alert destinations.

## Overview

The Alerting provider adds intelligent failure alerting to your workflows. When a workflow implementing `IAlertingWorkflow` fails, the system evaluates your configured conditions and sends alerts via all registered `IAlertSender` implementations.

## Features

- **Flexible Conditions**: Time windows, failure thresholds, exception types, step filters, and custom predicates
- **Single Database Query**: Optimized to minimize DB interaction
- **No-Query Optimization**: When `MinimumFailures == 1`, skips the database entirely
- **Multiple Alert Destinations**: Send to SNS, email, Slack, PagerDuty, etc. simultaneously
- **Debouncing**: Optional cooldown periods to prevent alert spam
- **Rich Context**: Comprehensive metadata passed to alert senders for intelligent routing
- **Compile-Time Safety**: Analyzer enforces required fields (future implementation)

## Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect.Provider.Alerting
```

## Quick Start

### 1. Implement IAlertSender

Create your alert destination (SNS, email, etc.):

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
        // Determine severity from exception type
        var severity = context.TriggerMetadata.FailureException switch
        {
            "TimeoutException" => "WARNING",
            "DatabaseException" => "CRITICAL",
            _ => "ERROR"
        };

        var message = $"Workflow {context.WorkflowName} failed {context.FailureCount} " +
                     $"times in the last {context.TimeWindow.TotalMinutes:F0} minutes.\n\n" +
                     $"Latest failure: {context.TriggerMetadata.FailureReason}";

        var topicArn = severity == "CRITICAL" 
            ? Environment.GetEnvironmentVariable("SNS_CRITICAL_ARN")
            : Environment.GetEnvironmentVariable("SNS_STANDARD_ARN");

        await _sns.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Subject = $"[{severity}] Workflow Alert: {context.WorkflowName}",
            Message = message
        }, cancellationToken);

        _logger.LogInformation(
            "Sent {Severity} alert for {Workflow}",
            severity, context.WorkflowName);
    }
}
```

### 2. Register the Alerting Effect

```csharp
services.AddChainSharpEffects(options =>
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()  // Required for FailedInputs in AlertContext
        .AddEffectWorkflowBus(typeof(Program).Assembly)
        .UseAlertingEffect(alertOptions =>
            alertOptions
                .AddAlertSender<SnsSender>()
                .WithDebouncing(TimeSpan.FromMinutes(15)))
);
```

### 3. Implement IAlertingWorkflow

```csharp
public class MySyncWorkflow : EffectWorkflow<SyncInput, Unit>, 
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

## Alert Configuration

### Required Fields

Both `WithinTimeSpan()` and `MinimumFailures()` must be called before `Build()`:

```csharp
// ✅ Valid
AlertConfigurationBuilder.Create()
    .WithinTimeSpan(TimeSpan.FromHours(1))
    .MinimumFailures(3)
    .Build();

// ❌ Compile error (with analyzer)
AlertConfigurationBuilder.Create()
    .WithinTimeSpan(TimeSpan.FromHours(1))
    .Build();  // Missing MinimumFailures()
```

### Convenience Method

For simple "alert on every failure" behavior:

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .AlertOnEveryFailure()
        .Build();
```

### Exception Type Filters

Alert only on specific exception types (OR logic):

```csharp
.WhereExceptionType<TimeoutException>()
.WhereExceptionType<DatabaseException>()
// Alerts if EITHER TimeoutException OR DatabaseException
```

### Step Name Filters

Filter by the step where the failure occurred (OR logic):

```csharp
// Exact match
.WhereFailureStepNameEquals("DatabaseConnectionStep")

// Custom predicate
.WhereFailureStepName(step => step.StartsWith("Database"))
.WhereFailureStepName(step => step.Contains("Connection"))
// Alerts if step name starts with "Database" OR contains "Connection"
```

### Custom Filters

Full control with access to the Metadata object (AND logic):

```csharp
.AndCustomFilter(m => m.FailureReason?.Contains("timeout") ?? false)
.AndCustomFilter(m => m.StartTime.Hour >= 9 && m.StartTime.Hour <= 17)
// BOTH conditions must be true
```

### Complete Example

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(5)
        .WhereExceptionType<TimeoutException>()
        .WhereFailureStepName(step => step.StartsWith("Database"))
        .AndCustomFilter(m => 
            m.FailureReason?.Contains("connection") ?? false)
        .Build();
```

## Alert Context

Your `IAlertSender` receives comprehensive context:

```csharp
public class AlertContext
{
    // Core Information
    public string WorkflowName { get; init; }
    public Metadata TriggerMetadata { get; init; }
    public int FailureCount { get; init; }
    public TimeSpan TimeWindow { get; init; }
    public AlertConfiguration Configuration { get; init; }

    // Historical Context
    public IReadOnlyList<Metadata> FailedExecutions { get; init; }
    public int TotalExecutions { get; init; }
    public DateTime FirstFailureTime { get; init; }
    public DateTime? LastSuccessTime { get; init; }

    // Failure Analysis
    public Dictionary<string, int> ExceptionFrequency { get; init; }
    public Dictionary<string, int> FailedStepFrequency { get; init; }
    public IReadOnlyList<string> FailedInputs { get; init; }
}
```

### Using AlertContext

```csharp
public async Task SendAlertAsync(AlertContext context, CancellationToken ct)
{
    // Calculate failure rate
    var failureRate = (double)context.FailureCount / context.TotalExecutions;
    
    // Find most common exception
    var mostCommon = context.ExceptionFrequency
        .OrderByDescending(kv => kv.Value)
        .FirstOrDefault();
    
    // Determine how long failures have been occurring
    var failureDuration = DateTime.UtcNow - context.FirstFailureTime;
    
    var message = $@"
Workflow: {context.WorkflowName}
Failures: {context.FailureCount} in {context.TimeWindow.TotalMinutes:F0} minutes
Failure Rate: {failureRate:P}
Most Common: {mostCommon.Key} ({mostCommon.Value}x)
Duration: {failureDuration.TotalMinutes:F1} minutes
Latest: {context.TriggerMetadata.FailureReason}
";
    
    await SendToDestination(message, ct);
}
```

## Multiple Alert Senders

Register as many senders as you need:

```csharp
.UseAlertingEffect(alertOptions =>
    alertOptions
        .AddAlertSender<SnsSender>()
        .AddAlertSender<EmailSender>()
        .AddAlertSender<SlackSender>()
        .AddAlertSender<PagerDutySender>()
        .WithDebouncing(TimeSpan.FromMinutes(30)))
```

All senders receive alerts. If one fails, others still execute.

## Debouncing

Prevent alert fatigue when workflows repeatedly fail:

```csharp
.UseAlertingEffect(alertOptions =>
    alertOptions
        .AddAlertSender<SnsSender>()
        .WithDebouncing(TimeSpan.FromMinutes(15)))
```

**Without debouncing:**
- Workflow fails 5 times in 10 minutes (MinimumFailures = 2)
- Alerts sent: 4 (at failure 2, 3, 4, and 5)

**With debouncing (15 min cooldown):**
- Workflow fails 5 times in 10 minutes (MinimumFailures = 2)
- Alerts sent: 1 (at failure 2, then cooldown suppresses 3, 4, 5)

## Performance Optimizations

1. **Single DB Query**: One optimized query per alert check (when MinimumFailures > 1)
2. **Skip Query for MinimumFailures == 1**: Immediate alert, no database hit
3. **In-Memory Filtering**: All filter logic happens after the query
4. **Only Triggers on Failure**: No overhead for successful workflows
5. **Cached Configurations**: ConfigureAlerting() called once at startup
6. **Memory-Based Debouncing**: No database interaction for cooldown state

## Metadata Field Reference

Fields available in `.AndCustomFilter()`:

| Field | Type | Description |
|-------|------|-------------|
| `Id` | int | Database ID |
| `Name` | string | Workflow name (fully qualified) |
| `ExternalId` | string | GUID for external references |
| `WorkflowState` | WorkflowState | Pending/InProgress/Completed/Failed |
| `StartTime` | DateTime | When execution started |
| `EndTime` | DateTime? | When execution finished |
| `FailureStep` | string? | Step where failure occurred |
| `FailureException` | string? | Exception type name |
| `FailureReason` | string? | Exception message |
| `StackTrace` | string? | Full stack trace |
| `Input` | string? | Serialized input (if SaveWorkflowParameters configured) |
| `Output` | string? | Serialized output |
| `ParentId` | int? | ID of parent workflow |
| `ManifestId` | int? | ID of scheduling manifest |
| `ScheduledTime` | DateTime? | When job was scheduled to run |

## Requirements

- **ChainSharp.Effect** 5.x or later
- **ChainSharp.Effect.Data** (optional, but required for MinimumFailures > 1)
- **SaveWorkflowParameters()** (optional, but required for FailedInputs in AlertContext)
- **AddEffectWorkflowBus()** (recommended, or pass assemblies explicitly)

## Advanced Usage

### Conditional Alert Routing

```csharp
public class SmartAlertSender : IAlertSender
{
    public async Task SendAlertAsync(AlertContext context, CancellationToken ct)
    {
        // Route based on time of day
        var hour = DateTime.UtcNow.Hour;
        var destination = hour >= 9 && hour <= 17 
            ? EmailDestination 
            : PagerDutyDestination;
        
        // Route based on failure rate
        var failureRate = (double)context.FailureCount / context.TotalExecutions;
        if (failureRate > 0.5)
        {
            await SendToCriticalChannel(context, ct);
        }
        else
        {
            await SendToStandardChannel(context, ct);
        }
    }
}
```

### Business Hours Only

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(3)
        .AndCustomFilter(m => 
        {
            var hour = m.StartTime.Hour;
            return hour >= 9 && hour <= 17;  // Only alert during business hours
        })
        .Build();
```

### Assembly Scanning

```csharp
// Explicit assemblies (recommended)
var assemblies = new[] { typeof(Program).Assembly, typeof(MyWorkflow).Assembly };

services.AddChainSharpEffects(options =>
    options
        .AddEffectWorkflowBus(assemblies)
        .UseAlertingEffect(
            alertOptions => alertOptions.AddAlertSender<SnsSender>(),
            assemblies)
);

// Inferred from WorkflowRegistry (convenient but less explicit)
services.AddChainSharpEffects(options =>
    options
        .AddEffectWorkflowBus(typeof(Program).Assembly)
        .UseAlertingEffect(alertOptions => 
            alertOptions.AddAlertSender<SnsSender>())
        // Will extract assemblies from WorkflowRegistry
);
```

## Troubleshooting

### "Alert never sent"

1. Check that your workflow implements `IAlertingWorkflow<TIn, TOut>`
2. Verify ConfigureAlerting() returns a valid configuration
3. Check logs for "Alert conditions not met" messages
4. Ensure at least one IAlertSender is registered
5. If MinimumFailures > 1, verify a data provider is configured

### "Could not register alert configuration"

This warning appears when a workflow has constructor dependencies. The workflow will still function normally, but alerting won't work. Consider:
- Adding a parameterless constructor (can call a private constructor with dependencies)
- Using property injection instead of constructor injection

### "Alert sent multiple times"

Enable debouncing to suppress repeated alerts:

```csharp
.WithDebouncing(TimeSpan.FromMinutes(15))
```

### Performance Issues

- Avoid expensive operations in custom filters (they run in-memory on every failure)
- Keep TimeWindow reasonable (larger windows = more data to query)
- Consider MinimumFailures == 1 if you don't need historical context

## Architecture

```
[Workflow Fails]
       ↓
[EffectWorkflow catches exception]
       ↓
[EffectRunner.OnError() called]
       ↓
[AlertingEffect.OnError() triggered]
       ↓
[Check: Is IAlertingWorkflow?] ─No→ [Skip]
       ↓ Yes
[Check: In cooldown?] ─Yes→ [Skip]
       ↓ No
[Check: MinimumFailures == 1?] ─Yes→ [Skip DB, send alert immediately]
       ↓ No
[Single DB Query: Get window data]
       ↓
[Apply filters in-memory]
       ↓
[Check: Conditions met?] ─No→ [Skip]
       ↓ Yes
[Build AlertContext]
       ↓
[Send to ALL IAlertSender implementations]
       ↓
[Set cooldown (if enabled)]
```

## Future Enhancements

- **Analyzer Package**: Compile-time validation of AlertConfigurationBuilder and AlertingOptionsBuilder (planned)
- **Dashboard Integration**: View alert history and configuration in ChainSharp Dashboard
- **Alert History**: Persist alert send events for auditing
- **Static ConfigureAlerting**: Support workflows with constructor dependencies

## License

MIT
