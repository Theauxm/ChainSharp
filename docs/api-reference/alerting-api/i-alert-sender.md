---
layout: default
title: IAlertSender
parent: Alerting API
grand_parent: API Reference
---

# IAlertSender

Interface for implementing custom alert destinations (SNS, email, Slack, PagerDuty, etc.).

## Signature

```csharp
public interface IAlertSender
```

## Methods

### SendAlertAsync(AlertContext, CancellationToken)

Sends an alert notification with the provided context.

```csharp
Task SendAlertAsync(AlertContext context, CancellationToken cancellationToken)
```

**Parameters:**
- `context` - Comprehensive context about the alert including failure details and historical data
- `cancellationToken` - Cancellation token for the operation

**Returns:** A task representing the asynchronous operation

**Remarks:**

**IMPORTANT:** This method should NOT throw exceptions. If alert sending fails, log the error and return gracefully. Throwing an exception will prevent other senders from being notified.

The method is called when a workflow's alert conditions are met. Implementations should:
1. Extract relevant information from the AlertContext
2. Format the alert message appropriately for the target system
3. Send the alert to the configured destination
4. Log success or failure of the send operation

## Implementation Examples

### AWS SNS Sender

```csharp
public class SnsSender : IAlertSender
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly ILogger<SnsSender> _logger;
    private readonly string _criticalTopicArn;
    private readonly string _standardTopicArn;

    public SnsSender(
        IAmazonSimpleNotificationService sns,
        ILogger<SnsSender> logger,
        IConfiguration config)
    {
        _sns = sns;
        _logger = logger;
        _criticalTopicArn = config["SNS:CriticalTopicArn"]!;
        _standardTopicArn = config["SNS:StandardTopicArn"]!;
    }

    public async Task SendAlertAsync(
        AlertContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Determine severity
            var severity = DetermineSeverity(context);
            var topicArn = severity == "CRITICAL" 
                ? _criticalTopicArn 
                : _standardTopicArn;

            // Format message
            var message = FormatAlertMessage(context, severity);

            // Send to SNS
            await _sns.PublishAsync(new PublishRequest
            {
                TopicArn = topicArn,
                Subject = $"[{severity}] Workflow Alert: {GetWorkflowShortName(context)}",
                Message = message
            }, cancellationToken);

            _logger.LogInformation(
                "Sent {Severity} alert for workflow {Workflow} to SNS topic {Topic}",
                severity, context.WorkflowName, topicArn);
        }
        catch (Exception ex)
        {
            // Log but don't throw - other senders should still be notified
            _logger.LogError(ex,
                "Failed to send SNS alert for workflow {Workflow}",
                context.WorkflowName);
        }
    }

    private string DetermineSeverity(AlertContext context)
    {
        var failureRate = (double)context.FailureCount / context.TotalExecutions;
        var hasDatabaseException = context.ExceptionFrequency
            .ContainsKey("DatabaseException");

        return failureRate > 0.5 || hasDatabaseException 
            ? "CRITICAL" 
            : "WARNING";
    }

    private string FormatAlertMessage(AlertContext context, string severity)
    {
        var failureDuration = DateTime.UtcNow - context.FirstFailureTime;
        var mostCommon = context.ExceptionFrequency
            .OrderByDescending(kv => kv.Value)
            .FirstOrDefault();

        return $@"
Workflow Alert Details:
- Workflow: {context.WorkflowName}
- Severity: {severity}
- Failures: {context.FailureCount} in {context.TimeWindow.TotalMinutes:F0} min
- Duration: {failureDuration.TotalMinutes:F1} minutes

Latest Failure:
- Exception: {context.TriggerMetadata.FailureException}
- Reason: {context.TriggerMetadata.FailureReason}
- Step: {context.TriggerMetadata.FailureStep}
- Time: {context.TriggerMetadata.StartTime:u}

Statistics:
- Most Common: {mostCommon.Key} ({mostCommon.Value}x)
- Failure Rate: {((double)context.FailureCount / context.TotalExecutions):P}
{(context.LastSuccessTime.HasValue 
    ? $"- Last Success: {context.LastSuccessTime.Value:u}" 
    : "- No recent successes")}
";
    }

    private string GetWorkflowShortName(AlertContext context) =>
        context.WorkflowName.Split('.').Last();
}
```

### Email Sender

```csharp
public class EmailSender : IAlertSender
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailSender> _logger;
    private readonly string _alertEmail;

    public EmailSender(
        IEmailService emailService,
        ILogger<EmailSender> logger,
        IConfiguration config)
    {
        _emailService = emailService;
        _logger = logger;
        _alertEmail = config["Alerts:EmailAddress"]!;
    }

    public async Task SendAlertAsync(
        AlertContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var subject = $"Workflow Alert: {context.WorkflowName}";
            var body = BuildHtmlEmail(context);

            await _emailService.SendEmailAsync(
                _alertEmail,
                subject,
                body,
                isHtml: true,
                cancellationToken);

            _logger.LogInformation(
                "Sent email alert for workflow {Workflow} to {Email}",
                context.WorkflowName, _alertEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email alert for workflow {Workflow}",
                context.WorkflowName);
        }
    }

    private string BuildHtmlEmail(AlertContext context)
    {
        // Build rich HTML email with tables, charts, etc.
        return $@"
<html>
<body>
    <h2>Workflow Failure Alert</h2>
    <p><strong>Workflow:</strong> {context.WorkflowName}</p>
    <p><strong>Failures:</strong> {context.FailureCount} in {context.TimeWindow.TotalMinutes:F0} minutes</p>
    
    <h3>Latest Failure</h3>
    <pre>{context.TriggerMetadata.FailureReason}</pre>
    
    <h3>Exception Breakdown</h3>
    <ul>
    {string.Join("", context.ExceptionFrequency.Select(kv => 
        $"<li>{kv.Key}: {kv.Value} occurrences</li>"))}
    </ul>
</body>
</html>";
    }
}
```

## Registration

Register implementations with the DI container via `UseAlertingEffect()`:

```csharp
services.AddChainSharpEffects(options =>
    options.UseAlertingEffect(alertOptions =>
        alertOptions
            .AddAlertSender<SnsSender>()
            .AddAlertSender<EmailSender>()
            .AddAlertSender<SlackSender>())
);
```

## Error Handling

Alert senders should handle errors gracefully to prevent one failure from blocking other senders:

```csharp
public async Task SendAlertAsync(AlertContext context, CancellationToken ct)
{
    try
    {
        await SendToDestination(context, ct);
        _logger.LogInformation("Alert sent successfully");
    }
    catch (Exception ex)
    {
        // Log but don't rethrow - other senders should still be notified
        _logger.LogError(ex, "Failed to send alert");
        // DO NOT: throw;
    }
}
```

## See Also

- [AlertContext](alert-context.md) - The context object passed to SendAlertAsync()
- [AlertingOptionsBuilder](alerting-options-builder.md) - How to register senders
- [Usage Guide: Alerting](../../usage-guide/alerting.md)
