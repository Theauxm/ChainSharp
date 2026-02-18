using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Provider.Alerting.Models;

/// <summary>
/// Comprehensive context information provided to IAlertSender implementations
/// when alert conditions are met.
/// </summary>
/// <remarks>
/// AlertContext contains rich information about the workflow failure that triggered
/// the alert, including historical context, exception details, and the configuration
/// that determined the alert should be sent.
///
/// This allows IAlertSender implementations to make intelligent decisions about:
/// - Alert severity/priority
/// - Routing to different channels
/// - Message formatting and content
/// - Deduplication logic
///
/// Example usage in an IAlertSender:
/// <code>
/// public async Task SendAlertAsync(AlertContext context, CancellationToken cancellationToken)
/// {
///     // Determine severity from exception frequency
///     var criticalExceptions = context.ExceptionFrequency
///         .Where(kv => kv.Key == "DatabaseException")
///         .Sum(kv => kv.Value);
///     
///     var severity = criticalExceptions > 0 ? "CRITICAL" : "WARNING";
///     
///     // Format message with context
///     var message = $"Workflow {context.WorkflowName} failed {context.FailureCount} times " +
///                  $"in the last {context.TimeWindow.TotalMinutes} minutes.\n\n" +
///                  $"Latest: {context.TriggerMetadata.FailureReason}\n" +
///                  $"Total runs: {context.TotalExecutions}";
///     
///     await SendToChannel(severity, message, cancellationToken);
/// }
/// </code>
/// </remarks>
public class AlertContext
{
    /// <summary>
    /// Gets the name of the workflow that triggered the alert.
    /// </summary>
    /// <remarks>
    /// This is the fully qualified type name of the workflow class.
    /// </remarks>
    public required string WorkflowName { get; init; }

    /// <summary>
    /// Gets the specific metadata record that triggered the alert evaluation.
    /// </summary>
    /// <remarks>
    /// This is the most recent failure that caused the alert system to check
    /// if conditions were met. It contains the exception, failure step, and
    /// other details of this specific execution.
    ///
    /// Note: This may not be the only failure in the time window - see
    /// FailedExecutions for the complete list.
    /// </remarks>
    public required Metadata TriggerMetadata { get; init; }

    /// <summary>
    /// Gets the number of failures that occurred in the configured time window.
    /// </summary>
    /// <remarks>
    /// This count includes only failures that satisfied all configured filters
    /// (exception type, step name, custom filters, etc.).
    ///
    /// This value will always be >= MinimumFailures since the alert would not
    /// have been triggered otherwise.
    /// </remarks>
    public required int FailureCount { get; init; }

    /// <summary>
    /// Gets the time window that was evaluated for failures.
    /// </summary>
    /// <remarks>
    /// This is the TimeWindow value from the AlertConfiguration.
    /// For MinimumFailures == 1, this is typically TimeSpan.Zero.
    /// </remarks>
    public required TimeSpan TimeWindow { get; init; }

    /// <summary>
    /// Gets the alert configuration that determined this alert should be sent.
    /// </summary>
    /// <remarks>
    /// This contains all the rules that were evaluated, including filters,
    /// thresholds, and time windows. Useful for including configuration details
    /// in alert messages or for conditional routing.
    /// </remarks>
    public required AlertConfiguration Configuration { get; init; }

    /// <summary>
    /// Gets the list of all failed executions in the time window that satisfied
    /// the alert conditions.
    /// </summary>
    /// <remarks>
    /// This list includes all Metadata records for failures that:
    /// 1. Occurred within the TimeWindow
    /// 2. Matched all configured filters
    ///
    /// This allows alert senders to analyze patterns, extract common failure
    /// reasons, or include multiple failure details in the alert message.
    ///
    /// For MinimumFailures == 1, this list typically contains only the
    /// TriggerMetadata.
    /// </remarks>
    public required IReadOnlyList<Metadata> FailedExecutions { get; init; }

    /// <summary>
    /// Gets the total number of workflow executions (both successful and failed)
    /// in the time window.
    /// </summary>
    /// <remarks>
    /// This includes ALL executions with the same workflow name in the time window,
    /// regardless of whether they passed filters or succeeded.
    ///
    /// Useful for calculating failure rates:
    /// <code>
    /// var failureRate = (double)context.FailureCount / context.TotalExecutions;
    /// </code>
    ///
    /// For MinimumFailures == 1, this value may be unreliable since the
    /// database query is skipped for performance.
    /// </remarks>
    public required int TotalExecutions { get; init; }

    /// <summary>
    /// Gets the timestamp of the first failure in the time window.
    /// </summary>
    /// <remarks>
    /// This can be used to determine how long the workflow has been experiencing
    /// failures.
    ///
    /// Example:
    /// <code>
    /// var failureDuration = DateTime.UtcNow - context.FirstFailureTime;
    /// var message = $"Workflow has been failing for {failureDuration.TotalMinutes:F1} minutes";
    /// </code>
    /// </remarks>
    public required DateTime FirstFailureTime { get; init; }

    /// <summary>
    /// Gets the timestamp of the last successful execution, if any occurred
    /// in the time window.
    /// </summary>
    /// <remarks>
    /// This can be null if no successful executions occurred in the time window
    /// or if MinimumFailures == 1 (database query skipped).
    ///
    /// Useful for determining when the workflow last worked correctly:
    /// <code>
    /// var message = context.LastSuccessTime.HasValue
    ///     ? $"Last success: {context.LastSuccessTime.Value:u}"
    ///     : "No recent successes";
    /// </code>
    /// </remarks>
    public required DateTime? LastSuccessTime { get; init; }

    /// <summary>
    /// Gets a dictionary of exception types and their occurrence counts.
    /// </summary>
    /// <remarks>
    /// This provides a summary of which exceptions occurred and how often.
    /// The keys are exception type names (e.g., "TimeoutException") and the
    /// values are the number of times each exception occurred.
    ///
    /// Useful for identifying the most common failure cause:
    /// <code>
    /// var mostCommon = context.ExceptionFrequency
    ///     .OrderByDescending(kv => kv.Value)
    ///     .First();
    /// var message = $"Most common: {mostCommon.Key} ({mostCommon.Value} times)";
    /// </code>
    /// </remarks>
    public required Dictionary<string, int> ExceptionFrequency { get; init; }

    /// <summary>
    /// Gets a dictionary of failed step names and their occurrence counts.
    /// </summary>
    /// <remarks>
    /// This provides a summary of which steps failed and how often.
    /// The keys are step names and the values are the number of times each
    /// step failed.
    ///
    /// Useful for identifying problematic steps:
    /// <code>
    /// var problematicSteps = context.FailedStepFrequency
    ///     .Where(kv => kv.Value > 1)
    ///     .Select(kv => $"{kv.Key} ({kv.Value}x)");
    /// </code>
    /// </remarks>
    public required Dictionary<string, int> FailedStepFrequency { get; init; }

    /// <summary>
    /// Gets the list of serialized input data from failed executions.
    /// </summary>
    /// <remarks>
    /// This contains the Input field (JSON string) from each failed Metadata record.
    /// Empty strings are excluded from the list.
    ///
    /// Useful for identifying patterns in failure inputs or including sample
    /// inputs in alert messages.
    ///
    /// Note: Inputs are only available if SaveWorkflowParameters() was configured.
    /// Otherwise, this list will be empty.
    ///
    /// Example:
    /// <code>
    /// if (context.FailedInputs.Any())
    /// {
    ///     var sampleInput = context.FailedInputs.First();
    ///     var message = $"Sample failed input: {sampleInput}";
    /// }
    /// </code>
    /// </remarks>
    public required IReadOnlyList<string> FailedInputs { get; init; }
}
