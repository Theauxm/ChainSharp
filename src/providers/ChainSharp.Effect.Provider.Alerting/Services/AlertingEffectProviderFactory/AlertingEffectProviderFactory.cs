using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Provider.Alerting.Interfaces;
using ChainSharp.Effect.Provider.Alerting.Models;
using ChainSharp.Effect.Provider.Alerting.Services.AlertConfigurationRegistry;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
// Alias to avoid namespace collision
using AlertingEffectProvider = ChainSharp.Effect.Provider.Alerting.Services.AlertingEffect.AlertingEffect;

namespace ChainSharp.Effect.Provider.Alerting.Services.AlertingEffectProviderFactory;

/// <summary>
/// Factory for creating AlertingEffect instances.
/// </summary>
/// <remarks>
/// This factory follows the IEffectProviderFactory pattern used by ChainSharp.Effect
/// to create effect providers. It resolves all dependencies from the DI container
/// and creates a new AlertingEffect instance.
/// </remarks>
public class AlertingEffectProviderFactory : IEffectProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the AlertingEffectProviderFactory.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    public AlertingEffectProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a new AlertingEffect instance with all dependencies resolved.
    /// </summary>
    /// <returns>A new IEffectProvider instance</returns>
    /// <remarks>
    /// This method resolves the following dependencies from the DI container:
    /// - IDataContext (optional - may be null if no data provider configured)
    /// - IMemoryCache (required for debounce state)
    /// - IEnumerable&lt;IAlertSender&gt; (all registered alert senders)
    /// - IAlertConfigurationRegistry (contains cached workflow configurations)
    /// - AlertingOptionsBuilder (contains debounce settings)
    /// - ILogger&lt;AlertingEffect&gt; (for diagnostic logging)
    ///
    /// The IDataContext is retrieved as optional to allow the alerting system
    /// to work without a database provider (though only MinimumFailures == 1
    /// configurations will work in that case).
    /// </remarks>
    public IEffectProvider Create()
    {
        var dataContext = _serviceProvider.GetService<IDataContext>();
        var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
        var alertSenders = _serviceProvider.GetServices<IAlertSender>();
        var configRegistry = _serviceProvider.GetRequiredService<IAlertConfigurationRegistry>();
        var options = _serviceProvider.GetRequiredService<AlertingOptionsBuilder>();
        var logger = _serviceProvider.GetRequiredService<ILogger<AlertingEffectProvider>>();

        return new AlertingEffectProvider(
            dataContext,
            cache,
            alertSenders,
            configRegistry,
            options,
            logger
        );
    }
}
