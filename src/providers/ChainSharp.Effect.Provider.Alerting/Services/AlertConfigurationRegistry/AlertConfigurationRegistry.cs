using System.Reflection;
using ChainSharp.Effect.Provider.Alerting.Interfaces;
using ChainSharp.Effect.Provider.Alerting.Models;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Provider.Alerting.Services.AlertConfigurationRegistry;

/// <summary>
/// Implementation of IAlertConfigurationRegistry that scans assemblies for
/// IAlertingWorkflow implementations and caches their configurations.
/// </summary>
/// <remarks>
/// This registry is populated once during application startup by calling
/// ScanAndRegister with the assemblies that contain workflows.
///
/// The scanning process:
/// 1. Finds all types implementing IAlertingWorkflow&lt;,&gt;
/// 2. Attempts to create a temporary instance of each workflow
/// 3. Calls ConfigureAlerting() on the instance
/// 4. Caches the result keyed by workflow type name
///
/// If a workflow cannot be instantiated (e.g., has constructor dependencies),
/// a warning is logged but scanning continues for other workflows.
/// </remarks>
public class AlertConfigurationRegistry : IAlertConfigurationRegistry
{
    private readonly Dictionary<string, AlertConfiguration> _configurations = new();
    private readonly ILogger<AlertConfigurationRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the AlertConfigurationRegistry.
    /// </summary>
    /// <param name="logger">Logger for recording scanning results and warnings</param>
    public AlertConfigurationRegistry(ILogger<AlertConfigurationRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans the provided assemblies for IAlertingWorkflow implementations
    /// and caches their alert configurations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for alerting workflows</param>
    /// <remarks>
    /// This method should be called once during application startup, typically
    /// from the UseAlertingEffect extension method.
    ///
    /// The method scans each assembly for classes that:
    /// 1. Are concrete (not abstract)
    /// 2. Implement IAlertingWorkflow&lt;,&gt;
    ///
    /// For each qualifying type, it attempts to:
    /// 1. Create an instance using Activator.CreateInstance (parameterless constructor)
    /// 2. Call the ConfigureAlerting() method
    /// 3. Cache the result
    ///
    /// If instantiation fails (e.g., the workflow has constructor dependencies),
    /// a warning is logged and that workflow is skipped. The workflow can still
    /// be used normally; it just won't have alerting enabled.
    ///
    /// Performance note: This method uses reflection but is only called once at
    /// startup, so the performance impact is negligible.
    /// </remarks>
    public void ScanAndRegister(IEnumerable<Assembly> assemblies)
    {
        var alertingWorkflowInterface = typeof(IAlertingWorkflow<,>);
        var scannedCount = 0;
        var registeredCount = 0;

        foreach (var assembly in assemblies)
        {
            _logger.LogDebug(
                "Scanning assembly {Assembly} for alerting workflows",
                assembly.GetName().Name
            );

            var alertingWorkflowTypes = assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t =>
                    t.GetInterfaces()
                        .Any(i =>
                            i.IsGenericType
                            && i.GetGenericTypeDefinition() == alertingWorkflowInterface
                        )
                );

            foreach (var workflowType in alertingWorkflowTypes)
            {
                scannedCount++;

                try
                {
                    // Attempt to create a temporary instance to call ConfigureAlerting()
                    // This requires a parameterless constructor
                    var instance = Activator.CreateInstance(workflowType);

                    // Find the ConfigureAlerting method
                    // It should be defined by the IAlertingWorkflow interface implementation
                    var configMethod = workflowType.GetMethod(
                        nameof(IAlertingWorkflow<object, object>.ConfigureAlerting)
                    );

                    if (configMethod == null)
                    {
                        _logger.LogWarning(
                            "Workflow {Workflow} implements IAlertingWorkflow but ConfigureAlerting method not found. Skipping.",
                            workflowType.Name
                        );
                        continue;
                    }

                    // Call ConfigureAlerting() and cache the result
                    var config = (AlertConfiguration?)configMethod.Invoke(instance, null);

                    if (config == null)
                    {
                        _logger.LogWarning(
                            "Workflow {Workflow} ConfigureAlerting returned null. Skipping.",
                            workflowType.Name
                        );
                        continue;
                    }

                    var workflowName =
                        workflowType.FullName
                        ?? throw new InvalidOperationException(
                            $"Could not determine full name for {workflowType.Name}"
                        );

                    _configurations[workflowName] = config;
                    registeredCount++;

                    _logger.LogDebug(
                        "Registered alert configuration for workflow {Workflow} " +
                        "(MinFailures: {MinFailures}, TimeWindow: {TimeWindow})",
                        workflowType.Name,
                        config.MinimumFailures,
                        config.TimeWindow
                    );
                }
                catch (MissingMethodException)
                {
                    // Workflow has no parameterless constructor
                    _logger.LogWarning(
                        "Could not register alert configuration for {Workflow}: " +
                        "No parameterless constructor found. The workflow will function normally " +
                        "but alerting will not be enabled. Consider adding a parameterless constructor " +
                        "or implementing ConfigureAlerting as a static method in future versions.",
                        workflowType.Name
                    );
                }
                catch (Exception ex)
                {
                    // Other instantiation or configuration errors
                    _logger.LogWarning(
                        ex,
                        "Failed to register alert configuration for {Workflow}: {Error}. " +
                        "The workflow will function normally but alerting will not be enabled.",
                        workflowType.Name,
                        ex.Message
                    );
                }
            }
        }

        _logger.LogInformation(
            "Alert configuration registry initialized. Scanned {ScannedCount} workflows, " +
            "registered {RegisteredCount} with alerting enabled.",
            scannedCount,
            registeredCount
        );
    }

    /// <inheritdoc />
    public AlertConfiguration? GetConfiguration(string workflowName)
    {
        return _configurations.TryGetValue(workflowName, out var config) ? config : null;
    }
}
