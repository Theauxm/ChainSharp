using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Services.EffectRunner;

/// <summary>
/// Coordinates multiple effect providers to track and persist workflow metadata.
/// This class acts as a facade over all registered effect providers, delegating
/// operations to each provider and managing their lifecycle.
/// </summary>
/// <remarks>
/// The EffectRunner is the central component for managing side effects in the ChainSharp.Effect system.
/// It's responsible for:
/// 1. Creating and managing effect providers through their factories
/// 2. Tracking models across all providers
/// 3. Persisting changes to all providers
/// 4. Properly disposing of providers when they're no longer needed
/// 
/// This design follows the Composite pattern, allowing multiple effect providers
/// to be treated as a single unit.
/// </remarks>
public class EffectRunner : IEffectRunner
{
    /// <summary>
    /// Collection of active effect providers that will process tracking and persistence operations.
    /// </summary>
    /// <remarks>
    /// Each provider in this collection represents a different storage or processing mechanism
    /// for workflow metadata (e.g., database, logging, etc.).
    /// </remarks>
    private List<IEffectProvider> ActiveEffectProviders { get; init; }

    /// <summary>
    /// Initializes a new instance of the EffectRunner with the specified effect provider factories.
    /// </summary>
    /// <param name="effectProviderFactories">Collection of factories that create effect providers</param>
    /// <remarks>
    /// During initialization, the runner:
    /// 1. Creates an empty list of active providers
    /// 2. Calls Create() on each factory to instantiate the providers
    /// 3. Adds all created providers to the active providers list
    /// 
    /// This approach follows the Factory pattern, allowing for flexible provider creation
    /// and configuration through dependency injection.
    /// </remarks>
    public EffectRunner(IEnumerable<IEffectProviderFactory> effectProviderFactories)
    {
        ActiveEffectProviders = [];

        ActiveEffectProviders.AddRange(effectProviderFactories.RunAll(factory => factory.Create()));
    }

    /// <summary>
    /// Persists any pending changes across all active effect providers.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method calls SaveChanges on each active provider in parallel,
    /// allowing for efficient persistence across multiple storage mechanisms.
    /// The RunAllAsync extension method ensures all providers are called
    /// regardless of exceptions in individual providers.
    /// </remarks>
    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await ActiveEffectProviders.RunAllAsync(
            provider => provider.SaveChanges(cancellationToken)
        );
    }

    /// <summary>
    /// Tracks a model across all active effect providers.
    /// </summary>
    /// <param name="model">The model to track</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method calls Track on each active provider,
    /// allowing the model to be processed by all registered providers.
    /// The RunAll extension method ensures all providers are called
    /// regardless of exceptions in individual providers.
    /// </remarks>
    public async Task Track(IModel model)
    {
        ActiveEffectProviders.RunAll(provider => provider.Track(model));
    }

    /// <summary>
    /// Disposes of all active effect providers and clears the collection.
    /// </summary>
    /// <remarks>
    /// This method ensures proper cleanup of resources used by effect providers.
    /// It's called automatically when the EffectRunner is disposed.
    /// </remarks>
    public void Dispose() => DeactivateProviders();

    /// <summary>
    /// Helper method to dispose of all active providers and clear the collection.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Calls Dispose on each active provider
    /// 2. Clears the collection of active providers
    /// 
    /// The RunAll extension method ensures all providers are disposed
    /// regardless of exceptions in individual providers.
    /// </remarks>
    private void DeactivateProviders()
    {
        ActiveEffectProviders.RunAll(provider => provider.Dispose());
        ActiveEffectProviders.Clear();
    }
}
