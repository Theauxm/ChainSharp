using System.Reflection;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Provider.Alerting.Interfaces;
using ChainSharp.Effect.Provider.Alerting.Models;
using ChainSharp.Effect.Provider.Alerting.Services.AlertConfigurationRegistry;
using ChainSharp.Effect.Provider.Alerting.Services.AlertingEffectProviderFactory;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Provider.Alerting.Extensions;

/// <summary>
/// Extension methods for registering the alerting effect provider.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Adds the alerting effect to the ChainSharp configuration.
    /// Scans the provided assemblies for IAlertingWorkflow implementations.
    /// </summary>
    /// <param name="builder">The ChainSharp effect configuration builder</param>
    /// <param name="configure">
    /// Action to configure alerting options.
    /// REQUIRED: Must call AddAlertSender&lt;T&gt;() at least once.
    /// The analyzer will enforce this at compile time.
    /// </param>
    /// <param name="assembliesToScan">
    /// Assemblies to scan for IAlertingWorkflow implementations.
    /// If not provided, attempts to infer from IWorkflowRegistry.
    /// </param>
    /// <returns>The configuration builder for method chaining</returns>
    /// <remarks>
    /// This method registers the alerting effect and scans for workflow alert configurations.
    ///
    /// Registration steps:
    /// 1. Validates that at least one alert sender is configured
    /// 2. Registers all IAlertSender implementations with DI (as Scoped)
    /// 3. Registers AlertingOptionsBuilder as Singleton
    /// 4. Creates and populates IAlertConfigurationRegistry with workflow configs
    /// 5. Registers IMemoryCache if not already present (for debouncing)
    /// 6. Registers AlertingEffectProviderFactory as an effect
    ///
    /// Example usage with explicit assemblies:
    /// <code>
    /// var assemblies = new[] { typeof(Program).Assembly };
    ///
    /// services.AddChainSharpEffects(options =>
    ///     options
    ///         .AddPostgresEffect(connectionString)
    ///         .SaveWorkflowParameters()
    ///         .AddEffectWorkflowBus(assemblies)
    ///         .UseAlertingEffect(
    ///             alertOptions => alertOptions
    ///                 .AddAlertSender&lt;SnsSender&gt;()
    ///                 .WithDebouncing(TimeSpan.FromMinutes(15)),
    ///             assemblies)
    /// );
    /// </code>
    ///
    /// Example usage with inferred assemblies:
    /// <code>
    /// services.AddChainSharpEffects(options =>
    ///     options
    ///         .AddPostgresEffect(connectionString)
    ///         .SaveWorkflowParameters()
    ///         .AddEffectWorkflowBus(typeof(Program).Assembly)
    ///         .UseAlertingEffect(alertOptions =>
    ///             alertOptions.AddAlertSender&lt;SnsSender&gt;())
    ///         // No assemblies passed - will extract from WorkflowRegistry
    /// );
    /// </code>
    ///
    /// IMPORTANT: The analyzer enforces that configure calls AddAlertSender at least once.
    /// </remarks>
    public static ChainSharpEffectConfigurationBuilder UseAlertingEffect(
        this ChainSharpEffectConfigurationBuilder builder,
        Action<AlertingOptionsBuilder> configure,
        params Assembly[] assembliesToScan
    )
    {
        var optionsBuilder = new AlertingOptionsBuilder();
        configure(optionsBuilder);

        // Runtime validation as fallback (analyzer should catch this at compile time)
        if (optionsBuilder.AlertSenderTypes.Count == 0)
            throw new InvalidOperationException(
                "At least one alert sender must be registered. "
                    + "Call options.AddAlertSender<TYourSender>() in the configure action."
            );

        // Register all alert senders as Scoped
        foreach (var senderType in optionsBuilder.AlertSenderTypes)
        {
            builder.ServiceCollection.AddScoped(typeof(IAlertSender), senderType);
        }

        // Register options as Singleton (shared across all effect instances)
        builder.ServiceCollection.AddSingleton(optionsBuilder);

        // Ensure IMemoryCache is registered (needed for debouncing)
        // This is idempotent - if already registered, this is a no-op
        builder.ServiceCollection.AddMemoryCache();

        // Register alert configuration registry
        builder.ServiceCollection.AddSingleton<IAlertConfigurationRegistry>(sp =>
        {
            var logger =
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AlertConfigurationRegistry>>();
            var registry = new AlertConfigurationRegistry(logger);

            // Determine which assemblies to scan
            Assembly[] assemblies;
            if (assembliesToScan.Length > 0)
            {
                // Use explicitly provided assemblies
                assemblies = assembliesToScan;
            }
            else
            {
                // Try to infer from WorkflowRegistry
                var workflowRegistry = sp.GetService<IWorkflowRegistry>();
                if (workflowRegistry != null)
                {
                    // Extract unique assemblies from registered workflow types
                    assemblies = workflowRegistry
                        .InputTypeToWorkflow.Values.Select(t => t.Assembly)
                        .Distinct()
                        .ToArray();
                }
                else
                {
                    throw new InvalidOperationException(
                        "No assemblies provided to UseAlertingEffect and AddEffectWorkflowBus not called. "
                            + "Either pass assemblies explicitly or call AddEffectWorkflowBus before UseAlertingEffect."
                    );
                }
            }

            // Scan assemblies and populate registry
            registry.ScanAndRegister(assemblies);
            return registry;
        });

        // Register the effect provider factory
        builder.AddEffect<AlertingEffectProviderFactory>();

        return builder;
    }
}
