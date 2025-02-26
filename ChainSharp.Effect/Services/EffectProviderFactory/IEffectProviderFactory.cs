using ChainSharp.Effect.Services.EffectProvider;

namespace ChainSharp.Effect.Services.EffectProviderFactory;

public interface IEffectProviderFactory
{
    public IEffectProvider Create();
}
