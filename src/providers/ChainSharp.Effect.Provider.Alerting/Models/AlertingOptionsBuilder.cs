using ChainSharp.Effect.Provider.Alerting.Interfaces;

namespace ChainSharp.Effect.Provider.Alerting.Models;

/// <summary>
/// Builder for configuring alerting effect options including alert senders and debouncing.
/// </summary>
/// <remarks>
/// AlertingOptionsBuilder is used when registering the alerting effect to configure:
/// 1. Which IAlertSender implementations should receive alerts (required - at least one)
/// 2. Whether debouncing should be enabled to prevent alert spam (optional)
///
/// The ChainSharp analyzer enforces that at least one alert sender is registered.
///
/// Example usage:
/// <code>
/// services.AddChainSharpEffects(options =>
///     options.UseAlertingEffect(alertOptions =>
///         alertOptions
///             .AddAlertSender&lt;SnsSender&gt;()
///             .AddAlertSender&lt;EmailSender&gt;()
///             .WithDebouncing(TimeSpan.FromMinutes(15)))
/// );
/// </code>
/// </remarks>
public class AlertingOptionsBuilder
{
    /// <summary>
    /// Gets the list of alert sender types that have been registered.
    /// </summary>
    /// <remarks>
    /// This list is populated by calls to AddAlertSender().
    /// All registered senders will receive alerts when conditions are met.
    /// </remarks>
    internal List<Type> AlertSenderTypes { get; } = new();

    /// <summary>
    /// Gets a value indicating whether debouncing is enabled.
    /// </summary>
    /// <remarks>
    /// When true, after an alert is sent for a workflow, subsequent alerts
    /// for the same workflow will be suppressed for the CooldownPeriod duration.
    /// </remarks>
    internal bool DebounceEnabled { get; private set; }

    /// <summary>
    /// Gets the cooldown period for debouncing, if enabled.
    /// </summary>
    /// <remarks>
    /// This is the duration that must pass after an alert is sent before
    /// another alert can be sent for the same workflow.
    ///
    /// Only has meaning when DebounceEnabled is true.
    /// </remarks>
    internal TimeSpan? CooldownPeriod { get; private set; }

    /// <summary>
    /// Registers a custom alert sender implementation.
    /// REQUIRED: Must be called at least once. The analyzer enforces this at compile time.
    /// Multiple senders can be registered - all will receive alerts.
    /// </summary>
    /// <typeparam name="TSender">The IAlertSender implementation type</typeparam>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// Each registered sender will be resolved from the DI container and called
    /// when alert conditions are met. Senders are called in the order they were
    /// registered.
    ///
    /// If one sender throws an exception, it will be logged but other senders
    /// will still be called.
    ///
    /// The sender type must implement IAlertSender and should be registered
    /// in the DI container (typically as Scoped).
    ///
    /// Example:
    /// <code>
    /// .AddAlertSender&lt;SnsSender&gt;()
    /// .AddAlertSender&lt;EmailSender&gt;()
    /// .AddAlertSender&lt;SlackSender&gt;()
    /// </code>
    /// </remarks>
    public AlertingOptionsBuilder AddAlertSender<TSender>()
        where TSender : class, IAlertSender
    {
        AlertSenderTypes.Add(typeof(TSender));
        return this;
    }

    /// <summary>
    /// Enables debouncing to prevent alert spam.
    /// After an alert is sent, subsequent alerts for the same workflow
    /// will be suppressed for the specified cooldown period.
    /// </summary>
    /// <param name="cooldownPeriod">
    /// The duration that must pass after an alert before another can be sent
    /// for the same workflow
    /// </param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// Debouncing prevents alert fatigue when a workflow is repeatedly failing.
    /// Once an alert is sent, a timer starts for that specific workflow. During
    /// the cooldown period, if the workflow fails again and meets alert conditions,
    /// no alert will be sent.
    ///
    /// The debounce state is stored in IMemoryCache, so it's local to each
    /// application instance (not shared across multiple servers).
    ///
    /// Example scenarios:
    /// 
    /// WITHOUT debouncing:
    /// - Workflow fails 5 times in 10 minutes (MinimumFailures = 2)
    /// - Alerts sent: 4 (at failure 2, 3, 4, and 5)
    ///
    /// WITH debouncing (15 minute cooldown):
    /// - Workflow fails 5 times in 10 minutes (MinimumFailures = 2)
    /// - Alerts sent: 1 (at failure 2, then cooldown prevents 3, 4, 5)
    ///
    /// Example:
    /// <code>
    /// .WithDebouncing(TimeSpan.FromMinutes(15))  // 15 minute cooldown
    /// .WithDebouncing(TimeSpan.FromHours(1))     // 1 hour cooldown
    /// </code>
    /// </remarks>
    public AlertingOptionsBuilder WithDebouncing(TimeSpan cooldownPeriod)
    {
        DebounceEnabled = true;
        CooldownPeriod = cooldownPeriod;
        return this;
    }
}
