using ChainSharp.Effect.Provider.Alerting.Models;

namespace ChainSharp.Effect.Provider.Alerting.Interfaces;

/// <summary>
/// Defines the contract for sending alert notifications when workflow failure conditions are met.
/// </summary>
/// <remarks>
/// IAlertSender implementations define where and how alerts are actually sent
/// (e.g., SNS, email, Slack, PagerDuty, etc.). Multiple senders can be registered
/// and all will receive alert notifications.
///
/// The implementation receives an AlertContext containing comprehensive information
/// about the failure, including historical context, exception details, and the
/// configuration that triggered the alert.
///
/// Example implementation:
/// <code>
/// public class SnsSender : IAlertSender
/// {
///     private readonly IAmazonSimpleNotificationService _sns;
///     private readonly ILogger&lt;SnsSender&gt; _logger;
///
///     public SnsSender(
///         IAmazonSimpleNotificationService sns,
///         ILogger&lt;SnsSender&gt; logger)
///     {
///         _sns = sns;
///         _logger = logger;
///     }
///
///     public async Task SendAlertAsync(
///         AlertContext context,
///         CancellationToken cancellationToken)
///     {
///         // Determine severity based on exception type
///         var severity = context.TriggerMetadata.FailureException switch
///         {
///             "TimeoutException" => "WARNING",
///             "DatabaseException" => "CRITICAL",
///             _ => "ERROR"
///         };
///
///         var message = $"Workflow {context.WorkflowName} failed {context.FailureCount} " +
///                      $"times in the last {context.TimeWindow.TotalMinutes} minutes.\n\n" +
///                      $"Latest failure: {context.TriggerMetadata.FailureReason}";
///
///         var topicArn = severity == "CRITICAL" 
///             ? "arn:aws:sns:us-east-1:123456789012:critical-alerts"
///             : "arn:aws:sns:us-east-1:123456789012:standard-alerts";
///
///         await _sns.PublishAsync(new PublishRequest
///         {
///             TopicArn = topicArn,
///             Subject = $"[{severity}] Workflow Alert: {context.WorkflowName}",
///             Message = message
///         }, cancellationToken);
///
///         _logger.LogInformation(
///             "Sent {Severity} alert for {Workflow} to SNS topic {Topic}",
///             severity, context.WorkflowName, topicArn);
///     }
/// }
/// </code>
/// 
/// Registration:
/// <code>
/// services.AddChainSharpEffects(options =>
///     options.UseAlertingEffect(alertOptions =>
///         alertOptions.AddAlertSender&lt;SnsSender&gt;())
/// );
/// </code>
/// </remarks>
public interface IAlertSender
{
    /// <summary>
    /// Sends an alert notification with the provided context.
    /// </summary>
    /// <param name="context">
    /// Comprehensive context about the alert, including the triggering failure,
    /// historical failures, exception details, and alert configuration
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method is called when a workflow's alert conditions are met.
    /// Implementations should:
    /// 
    /// 1. Extract relevant information from the AlertContext
    /// 2. Format the alert message appropriately for the target system
    /// 3. Send the alert to the configured destination
    /// 4. Log success or failure of the send operation
    ///
    /// IMPORTANT: This method should NOT throw exceptions. If alert sending fails,
    /// log the error and return gracefully. Throwing an exception will not retry
    /// the alert and may prevent other senders from being notified.
    ///
    /// The AlertContext provides rich information including:
    /// - TriggerMetadata: The specific execution that triggered the alert
    /// - FailedExecutions: All failures in the time window
    /// - ExceptionFrequency: Which exceptions occurred and how often
    /// - FailedStepFrequency: Which steps failed and how often
    /// - TotalExecutions: Both successful and failed runs in the window
    ///
    /// Use this information to make intelligent routing decisions, set alert severity,
    /// or include relevant details in the alert message.
    /// </remarks>
    Task SendAlertAsync(AlertContext context, CancellationToken cancellationToken);
}
