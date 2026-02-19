using System.Text.Json;
using ChainSharp.Effect.Provider.Alerting.Interfaces;
using ChainSharp.Effect.Provider.Alerting.Models;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlertExample;

/// <summary>
/// Example IAlertSender implementation for demonstration purposes.
/// Stores alert context in a public static variable and logs comprehensive details.
/// </summary>
public class ExampleAlertSender : IAlertSender
{
    private readonly ILogger<ExampleAlertSender> _logger;

    /// <summary>
    /// The most recent AlertContext received by this sender.
    /// Public static for easy inspection in demos and tests.
    /// </summary>
    public static AlertContext? LastAlertContext { get; private set; }

    /// <summary>
    /// Counter for total alerts sent.
    /// </summary>
    public static int TotalAlertsSent { get; private set; }

    public ExampleAlertSender(ILogger<ExampleAlertSender> logger)
    {
        _logger = logger;
    }

    public Task SendAlertAsync(AlertContext context, CancellationToken cancellationToken)
    {
        // Store context in static variable for inspection
        LastAlertContext = context;
        TotalAlertsSent++;

        // Log comprehensive alert details
        _logger.LogWarning(
            """
            
            ═══════════════════════════════════════════════════════════════
            🚨 WORKFLOW ALERT #{AlertNumber}
            ═══════════════════════════════════════════════════════════════
            
            Workflow: {WorkflowName}
            Failures: {FailureCount} in {TimeWindowMinutes:F0} minutes
            Total Executions: {TotalExecutions}
            Failure Rate: {FailureRate:P}
            
            ───────────────────────────────────────────────────────────────
            Latest Failure:
            ───────────────────────────────────────────────────────────────
            Time: {TriggerTime:u}
            Exception: {TriggerException}
            Reason: {TriggerReason}
            Step: {TriggerStep}
            
            ───────────────────────────────────────────────────────────────
            Failure Analysis:
            ───────────────────────────────────────────────────────────────
            Duration: {FailureDurationMinutes:F1} minutes
            First Failure: {FirstFailureTime:u}
            Last Success: {LastSuccess}
            
            Exception Frequency:
            {ExceptionFrequency}
            
            Failed Step Frequency:
            {FailedStepFrequency}
            
            ───────────────────────────────────────────────────────────────
            Alert Configuration:
            ───────────────────────────────────────────────────────────────
            Time Window: {ConfigTimeWindow}
            Minimum Failures: {ConfigMinFailures}
            Exception Filters: {ConfigExceptionFilters}
            Step Filters: {ConfigStepFilters}
            Custom Filters: {ConfigCustomFilters}
            
            ───────────────────────────────────────────────────────────────
            Failed Inputs ({FailedInputCount}):
            ───────────────────────────────────────────────────────────────
            {FailedInputsSample}
            
            ═══════════════════════════════════════════════════════════════
            
            """,
            TotalAlertsSent,
            context.WorkflowName,
            context.FailureCount,
            context.TimeWindow.TotalMinutes,
            context.TotalExecutions,
            context.TotalExecutions > 0
                ? (double)context.FailureCount / context.TotalExecutions
                : 0.0,
            context.TriggerMetadata.StartTime,
            context.TriggerMetadata.FailureException ?? "Unknown",
            context.TriggerMetadata.FailureReason ?? "No reason provided",
            context.TriggerMetadata.FailureStep ?? "Unknown",
            (DateTime.UtcNow - context.FirstFailureTime).TotalMinutes,
            context.FirstFailureTime,
            context.LastSuccessTime.HasValue
                ? context.LastSuccessTime.Value.ToString("u")
                : "No recent successes",
            FormatFrequency(context.ExceptionFrequency),
            FormatFrequency(context.FailedStepFrequency),
            context.Configuration.TimeWindow,
            context.Configuration.MinimumFailures,
            context.Configuration.ExceptionTypes.Count > 0
                ? string.Join(", ", context.Configuration.ExceptionTypes.Select(t => t.Name))
                : "None",
            context.Configuration.StepFilters.Count,
            context.Configuration.CustomFilters.Count,
            context.FailedInputs.Count,
            FormatFailedInputs(context.FailedInputs)
        );

        return Task.CompletedTask;
    }

    private static string FormatFrequency(Dictionary<string, int> frequency)
    {
        if (frequency.Count == 0)
            return "  (none)";

        return string.Join(
            "\n",
            frequency
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"  - {kv.Key}: {kv.Value}x")
        );
    }

    private static string FormatFailedInputs(IReadOnlyList<string> inputs)
    {
        if (inputs.Count == 0)
            return "  (none - SaveWorkflowParameters may not be configured)";

        // Show first 3 inputs
        var sample = inputs.Take(3).Select((input, i) => $"  [{i + 1}] {input}");

        if (inputs.Count > 3)
            return string.Join("\n", sample) + $"\n  ... and {inputs.Count - 3} more";

        return string.Join("\n", sample);
    }

    /// <summary>
    /// Resets the static state for testing purposes.
    /// </summary>
    public static void Reset()
    {
        LastAlertContext = null;
        TotalAlertsSent = 0;
    }
}
