using ChainSharp.Effect.Provider.Alerting.Models;
using ChainSharp.Effect.Services.EffectWorkflow;

namespace ChainSharp.Effect.Provider.Alerting.Interfaces;

/// <summary>
/// Defines a workflow that supports alert notifications on failure.
/// Workflows implementing this interface can configure conditions under which alerts should be sent.
/// </summary>
/// <typeparam name="TIn">The input type for the workflow</typeparam>
/// <typeparam name="TOut">The output type for the workflow</typeparam>
/// <remarks>
/// IAlertingWorkflow extends IEffectWorkflow to add alerting capabilities.
/// When a workflow implementing this interface fails, the alerting system will:
/// 1. Call the ConfigureAlerting() method to get the alert conditions
/// 2. Query the database for recent failures matching the workflow name
/// 3. Evaluate whether the configured conditions are met
/// 4. Send alerts via all registered IAlertSender implementations if conditions are satisfied
///
/// The ConfigureAlerting() method is called once during application startup and the result
/// is cached, so it should not perform any expensive operations or depend on runtime state.
///
/// Example implementation:
/// <code>
/// public class MySyncWorkflow : EffectWorkflow&lt;SyncInput, Unit&gt;, 
///     IAlertingWorkflow&lt;SyncInput, Unit&gt;
/// {
///     public AlertConfiguration ConfigureAlerting() =>
///         AlertConfigurationBuilder.Create()
///             .WithinTimeSpan(TimeSpan.FromHours(1))
///             .MinimumFailures(3)
///             .WhereExceptionType&lt;TimeoutException&gt;()
///             .Build();
///
///     protected override Task&lt;Either&lt;Exception, Unit&gt;&gt; RunInternal(SyncInput input) =>
///         Activate(input)
///             .Chain&lt;FetchDataStep&gt;()
///             .Chain&lt;ProcessDataStep&gt;()
///             .Resolve();
/// }
/// </code>
/// </remarks>
public interface IAlertingWorkflow<TIn, TOut> : IEffectWorkflow<TIn, TOut>
{
    /// <summary>
    /// Configures the conditions under which this workflow should send alerts.
    /// This method is called once during workflow registration and the result is cached.
    /// </summary>
    /// <returns>An AlertConfiguration defining when alerts should be sent</returns>
    /// <remarks>
    /// This method defines the rules that determine when an alert should be triggered
    /// for this workflow. The configuration is evaluated each time the workflow fails.
    ///
    /// IMPORTANT: This method is called during application startup, not during workflow
    /// execution. It should not depend on runtime state or perform expensive operations.
    ///
    /// The returned configuration must satisfy the following requirements:
    /// - TimeWindow must be set (use WithinTimeSpan() or AlertOnEveryFailure())
    /// - MinimumFailures must be set (use MinimumFailures() or AlertOnEveryFailure())
    ///
    /// The ChainSharp analyzer will enforce these requirements at compile time.
    /// </remarks>
    AlertConfiguration ConfigureAlerting();
}
