---
layout: default
title: AlertConfigurationBuilder
parent: Alerting API
grand_parent: API Reference
---

# AlertConfigurationBuilder

Fluent builder for creating `AlertConfiguration` instances that define when a workflow should send alerts.

## Signature

```csharp
public class AlertConfigurationBuilder
```

## Static Methods

### Create()

Creates a new `AlertConfigurationBuilder` instance.

```csharp
public static AlertConfigurationBuilder Create()
```

**Returns:** A new builder instance

**Example:**
```csharp
var config = AlertConfigurationBuilder.Create()
    .WithinTimeSpan(TimeSpan.FromHours(1))
    .MinimumFailures(3)
    .Build();
```

## Required Configuration Methods

### WithinTimeSpan(TimeSpan)

Sets the time window to evaluate for alert conditions. **REQUIRED** before `Build()`.

```csharp
public AlertConfigurationBuilder WithinTimeSpan(TimeSpan window)
```

**Parameters:**
- `window` - The time span to look back when checking for failures

**Returns:** This builder for method chaining

**Remarks:** Only failures that occurred within this time window from the current time will be considered. For `MinimumFailures == 1`, this value is typically `TimeSpan.Zero` since historical failures are not needed.

**Example:**
```csharp
.WithinTimeSpan(TimeSpan.FromHours(1))    // Last hour
.WithinTimeSpan(TimeSpan.FromMinutes(30)) // Last 30 minutes
.WithinTimeSpan(TimeSpan.FromDays(1))     // Last day
```

---

### MinimumFailures(int)

Sets the minimum number of failures required to trigger an alert. **REQUIRED** before `Build()`.

```csharp
public AlertConfigurationBuilder MinimumFailures(int count)
```

**Parameters:**
- `count` - The minimum failure count (must be >= 1)

**Returns:** This builder for method chaining

**Throws:** `ArgumentException` if count is less than 1

**Remarks:** When set to 1, the alert system optimizes by skipping the database query and sending an alert immediately.

**Example:**
```csharp
.MinimumFailures(1)  // Alert on every failure
.MinimumFailures(3)  // Alert after 3 failures
.MinimumFailures(10) // Alert after 10 failures
```

---

### AlertOnEveryFailure()

Convenience method that satisfies both `WithinTimeSpan()` and `MinimumFailures()` requirements.

```csharp
public AlertConfigurationBuilder AlertOnEveryFailure()
```

**Returns:** This builder for method chaining

**Remarks:** Equivalent to calling `.WithinTimeSpan(TimeSpan.Zero).MinimumFailures(1)`. This skips the database query for maximum performance.

**Example:**
```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .AlertOnEveryFailure()
        .Build();
```

## Optional Filter Methods

### WhereExceptionType&lt;TException&gt;()

Adds an exception type filter. Only failures with this exception type will be considered. Multiple calls use OR logic.

```csharp
public AlertConfigurationBuilder WhereExceptionType<TException>()
    where TException : Exception
```

**Type Parameters:**
- `TException` - The exception type to filter on

**Returns:** This builder for method chaining

**Remarks:** When one or more exception types are specified, only failures caused by these exceptions will be considered. Multiple types are combined with OR logic.

**Example:**
```csharp
.WhereExceptionType<TimeoutException>()
.WhereExceptionType<DatabaseException>()
// Alerts on EITHER TimeoutException OR DatabaseException
```

---

### WhereFailureStepNameEquals(string)

Adds a step name filter with exact equality comparison.

```csharp
public AlertConfigurationBuilder WhereFailureStepNameEquals(string stepName)
```

**Parameters:**
- `stepName` - The exact step name to match

**Returns:** This builder for method chaining

**Remarks:** Performs exact string match on the `FailureStep` property. Multiple calls use OR logic.

**Example:**
```csharp
.WhereFailureStepNameEquals("DatabaseConnectionStep")
.WhereFailureStepNameEquals("ApiCallStep")
// Alerts if EITHER step failed
```

---

### WhereFailureStepName(Func&lt;string, bool&gt;)

Adds a step name filter with custom predicate.

```csharp
public AlertConfigurationBuilder WhereFailureStepName(Func<string, bool> predicate)
```

**Parameters:**
- `predicate` - Function that takes a step name and returns true to include it

**Returns:** This builder for method chaining

**Remarks:** Allows complex step name matching without wildcards. Multiple calls use OR logic.

**Example:**
```csharp
.WhereFailureStepName(step => step.StartsWith("Database"))
.WhereFailureStepName(step => step.EndsWith("Step"))
.WhereFailureStepName(step => step.Contains("Connection"))
// Alerts if ANY predicate matches
```

---

### AndCustomFilter(Func&lt;Metadata, bool&gt;)

Adds a custom filter predicate over the Metadata object.

```csharp
public AlertConfigurationBuilder AndCustomFilter(Func<Metadata, bool> filter)
```

**Parameters:**
- `filter` - Function that takes a Metadata object and returns true to include it

**Returns:** This builder for method chaining

**Remarks:** Provides maximum flexibility by giving access to the full Metadata object. Multiple calls use AND logic - ALL must return true.

**Important:** These filters execute in-memory after the database query. Keep them lightweight to avoid performance issues.

**Example:**
```csharp
.AndCustomFilter(m => m.FailureReason?.Contains("timeout") ?? false)
.AndCustomFilter(m => m.StartTime.Hour >= 9 && m.StartTime.Hour <= 17)
.AndCustomFilter(m => m.ParentId == null)  // No nested workflows
// ALL conditions must be true
```

## Build Method

### Build()

Builds the final `AlertConfiguration`. **REQUIRED:** Must call `WithinTimeSpan()` and `MinimumFailures()` OR `AlertOnEveryFailure()` first.

```csharp
public AlertConfiguration Build()
```

**Returns:** A configured `AlertConfiguration` instance

**Throws:** 
- `InvalidOperationException` if `TimeWindow` not set
- `InvalidOperationException` if `MinimumFailures` not set

**Remarks:** The ChainSharp analyzer (ALERT001) enforces these requirements at compile time. Runtime validation is a fallback.

## Complete Example

```csharp
public AlertConfiguration ConfigureAlerting() =>
    AlertConfigurationBuilder.Create()
        .WithinTimeSpan(TimeSpan.FromHours(1))
        .MinimumFailures(5)
        .WhereExceptionType<TimeoutException>()
        .WhereExceptionType<SocketException>()
        .WhereFailureStepName(step => step.StartsWith("Database"))
        .AndCustomFilter(m => 
            m.FailureReason?.Contains("connection") ?? false)
        .AndCustomFilter(m => 
        {
            var hour = m.StartTime.Hour;
            return hour >= 9 && hour <= 17;  // Business hours only
        })
        .Build();
```

## See Also

- [AlertConfiguration](alert-configuration.md) - The result of Build()
- [IAlertingWorkflow](i-alerting-workflow.md) - How to use ConfigureAlerting()
- [ALERT001](alert001.md) - Analyzer that enforces required fields
- [Usage Guide: Alerting](../../usage-guide/alerting.md)
