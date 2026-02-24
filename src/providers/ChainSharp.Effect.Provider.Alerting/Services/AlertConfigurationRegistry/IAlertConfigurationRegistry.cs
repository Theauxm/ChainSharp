using ChainSharp.Effect.Provider.Alerting.Models;

namespace ChainSharp.Effect.Provider.Alerting.Services.AlertConfigurationRegistry;

/// <summary>
/// Provides access to cached alert configurations for workflows.
/// </summary>
/// <remarks>
/// The alert configuration registry scans assemblies during application startup
/// to find all IAlertingWorkflow implementations, calls their ConfigureAlerting()
/// methods, and caches the results.
///
/// This eliminates the need for reflection and method calls on every workflow failure,
/// significantly improving performance.
/// </remarks>
public interface IAlertConfigurationRegistry
{
    /// <summary>
    /// Gets the alert configuration for a workflow by its name.
    /// Returns null if the workflow doesn't implement IAlertingWorkflow.
    /// </summary>
    /// <param name="workflowName">The fully qualified type name of the workflow</param>
    /// <returns>
    /// The cached AlertConfiguration for the workflow, or null if the workflow
    /// does not implement IAlertingWorkflow or was not found during scanning
    /// </returns>
    /// <remarks>
    /// This method performs a fast dictionary lookup using the workflow name as the key.
    /// The workflow name should be the FullName property of the workflow's Type.
    ///
    /// Returns null in the following cases:
    /// - The workflow does not implement IAlertingWorkflow
    /// - The workflow was not found in the scanned assemblies
    /// - The workflow could not be instantiated during scanning (constructor dependencies)
    /// </remarks>
    AlertConfiguration? GetConfiguration(string workflowName);
}
