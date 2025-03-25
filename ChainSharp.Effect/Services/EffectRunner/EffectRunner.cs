using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Services.EffectRunner;

public class EffectRunner : IEffectRunner
{
    private List<IEffectProvider> ActiveEffectProviders { get; init; }

    public EffectRunner(IEnumerable<IEffectProviderFactory> effectProviderFactories)
    {
        ActiveEffectProviders = [];

        ActiveEffectProviders.AddRange(effectProviderFactories.RunAll(factory => factory.Create()));
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await ActiveEffectProviders.RunAllAsync(
            provider => provider.SaveChanges(cancellationToken)
        );
    }

    public async Task Track(IModel model)
    {
        ActiveEffectProviders.RunAll(provider => provider.Track(model));
    }

    public void Dispose() => DeactivateProviders();

    private void DeactivateProviders()
    {
        ActiveEffectProviders.RunAll(provider => provider.Dispose());
        ActiveEffectProviders.Clear();
    }
}
