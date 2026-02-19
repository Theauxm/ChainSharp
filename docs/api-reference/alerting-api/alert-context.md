---
layout: default
title: AlertContext
parent: Alerting API
grand_parent: API Reference
---

# AlertContext

Comprehensive context information provided to `IAlertSender` implementations when alert conditions are met.

## Signature

```csharp
public class AlertContext
```

## Properties

### WorkflowName

Gets the fully qualified type name of the workflow that triggered the alert.

```csharp
public required string WorkflowName { get; init; }
```

**Example Value:** `"MyCompany.Workflows.DataSyncWorkflow"`

---

### TriggerMetadata

Gets the specific metadata record that triggered the alert evaluation.

```csharp
public required Metadata TriggerMetadata { get; init; }
```

**Remarks:** This is the most recent failure that caused the alert system to evaluate conditions. It may not be the only failure in the time window - see `FailedExecutions` for the complete list.

---

### FailureCount

Gets the number of failures that satisfied the alert conditions in the time window.

```csharp
public required int FailureCount { get; init; }
```

**Remarks:** This count includes only failures that matched all configured filters. Will always be >= `MinimumFailures`.

---

### TimeWindow

Gets the time window that was evaluated for failures.

```csharp
public required TimeSpan TimeWindow { get; init; }
```

**Remarks:** For `MinimumFailures == 1`, this is typically `TimeSpan.Zero`.

---

### Configuration

Gets the alert configuration that determined this alert should be sent.

```csharp
public required AlertConfiguration Configuration { get; init; }
```

**Remarks:** Contains all the rules that were evaluated. Useful for including configuration details in alert messages.

---

### FailedExecutions

Gets all failed executions in the time window that satisfied the alert conditions.

```csharp
public required IReadOnlyList<Metadata> FailedExecutions { get; init; }
```

**Remarks:** Allows alert senders to analyze patterns or include multiple failure details. For `MinimumFailures == 1`, typically contains only `TriggerMetadata`.

---

### TotalExecutions

Gets the total number of workflow executions (successful + failed) in the time window.

```csharp
public required int TotalExecutions { get; init; }
```

**Remarks:** Useful for calculating failure rates. For `MinimumFailures == 1`, may be unreliable since the database query is skipped.

**Example:**
```csharp
var failureRate = (double)context.FailureCount / context.TotalExecutions;
if (failureRate > 0.5)
{
    // More than half of executions failed
    severity = "CRITICAL";
}
```

---

### FirstFailureTime

Gets the timestamp of the first failure in the time window.

```csharp
public required DateTime FirstFailureTime { get; init; }
```

**Example:**
```csharp
var failureDuration = DateTime.UtcNow - context.FirstFailureTime;
var message = $"Workflow has been failing for {failureDuration.TotalMinutes:F1} minutes";
```

---

### LastSuccessTime

Gets the timestamp of the last successful execution, if any occurred in the time window.

```csharp
public required DateTime? LastSuccessTime { get; init; }
```

**Remarks:** Can be null if no successful executions occurred or if `MinimumFailures == 1` (database query skipped).

**Example:**
```csharp
var timeSinceSuccess = context.LastSuccessTime.HasValue
    ? DateTime.UtcNow - context.LastSuccessTime.Value
    : null;

var message = timeSinceSuccess.HasValue
    ? $"Last success: {timeSinceSuccess.Value.TotalHours:F1} hours ago"
    : "No recent successes";
```

---

### ExceptionFrequency

Gets a dictionary of exception types and their occurrence counts.

```csharp
public required Dictionary<string, int> ExceptionFrequency { get; init; }
```

**Remarks:** Keys are exception type names (e.g., "TimeoutException"), values are counts.

**Example:**
```csharp
var mostCommon = context.ExceptionFrequency
    .OrderByDescending(kv => kv.Value)
    .FirstOrDefault();

var message = $"Most common exception: {mostCommon.Key} ({mostCommon.Value} times)";

// Check for specific exceptions
if (context.ExceptionFrequency.ContainsKey("DatabaseException"))
{
    severity = "CRITICAL";
}
```

---

### FailedStepFrequency

Gets a dictionary of failed step names and their occurrence counts.

```csharp
public required Dictionary<string, int> FailedStepFrequency { get; init; }
```

**Remarks:** Keys are step names, values are counts.

**Example:**
```csharp
var problematicSteps = context.FailedStepFrequency
    .Where(kv => kv.Value > context.FailureCount / 2)
    .Select(kv => $"{kv.Key} ({kv.Value}x)")
    .ToList();

var message = $"Problematic steps: {string.Join(", ", problematicSteps)}";
```

---

### FailedInputs

Gets the list of serialized input data from failed executions.

```csharp
public required IReadOnlyList<string> FailedInputs { get; init; }
```

**Remarks:** Contains JSON strings from the `Input` field of failed Metadata records. Empty strings are excluded. Only available if `SaveWorkflowParameters()` was configured.

**Example:**
```csharp
if (context.FailedInputs.Any())
{
    var sampleInput = context.FailedInputs.First();
    var message = $"Sample failed input: {sampleInput}";
    
    // Parse to find patterns
    var inputs = context.FailedInputs
        .Select(json => JsonSerializer.Deserialize<MyInputType>(json))
        .ToList();
}
```

## Usage Example

```csharp
public class SnsSender : IAlertSender
{
    public async Task SendAlertAsync(AlertContext context, CancellationToken ct)
    {
        // Calculate severity from failure rate and exception types
        var failureRate = (double)context.FailureCount / context.TotalExecutions;
        var hasCriticalException = context.ExceptionFrequency
            .ContainsKey("DatabaseException");
        
        var severity = (failureRate > 0.5 || hasCriticalException) 
            ? "CRITICAL" 
            : "WARNING";
        
        // Build comprehensive message
        var mostCommonException = context.ExceptionFrequency
            .OrderByDescending(kv => kv.Value)
            .FirstOrDefault();
        
        var failureDuration = DateTime.UtcNow - context.FirstFailureTime;
        
        var message = $@"
Workflow: {context.WorkflowName}
Severity: {severity}
Failures: {context.FailureCount} in {context.TimeWindow.TotalMinutes:F0} minutes
Failure Rate: {failureRate:P}
Duration: {failureDuration.TotalMinutes:F1} minutes

Most Common Exception: {mostCommonException.Key} ({mostCommonException.Value}x)
Latest Failure: {context.TriggerMetadata.FailureReason}
Failed Step: {context.TriggerMetadata.FailureStep}

{(context.LastSuccessTime.HasValue 
    ? $"Last Success: {context.LastSuccessTime.Value:u}" 
    : "No recent successes")}
";

        await SendToSns(severity, message, ct);
    }
}
```

## See Also

- [IAlertSender](i-alert-sender.md) - Interface that receives AlertContext
- [Metadata Reference](../../concepts/metadata.md) - Fields available in TriggerMetadata and FailedExecutions
- [Usage Guide: Alerting](../../usage-guide/alerting.md)
