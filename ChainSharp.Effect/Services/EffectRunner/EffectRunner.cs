using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Services.EffectRunner;

public class EffectRunner(IEnumerable<IEffectProviderFactory> effectProviderFactories)
    : IEffectRunner
{
    private List<IEffectProvider> ActiveEffectProviders { get; } = [];

    private bool HasActiveEffectProviders => ActiveEffectProviders.Count > 0;

    public async Task SaveChanges()
    {
        ActivateProviders();

        await ActiveEffectProviders.RunAllAsync(provider => provider.SaveChanges());
    }

    public async Task Track(IModel model)
    {
        ActivateProviders();

        ActiveEffectProviders.RunAll(provider => provider.Track(model));
    }

    public void Dispose() => DeactivateProviders();

    private void ActivateProviders()
    {
        if (HasActiveEffectProviders == false)
            ActiveEffectProviders.AddRange(
                effectProviderFactories.RunAll(factory => factory.Create())
            );
    }

    private void DeactivateProviders()
    {
        if (HasActiveEffectProviders)
            ActiveEffectProviders.RunAll(provider => provider.Dispose());

        ActiveEffectProviders.Clear();
    }
}
