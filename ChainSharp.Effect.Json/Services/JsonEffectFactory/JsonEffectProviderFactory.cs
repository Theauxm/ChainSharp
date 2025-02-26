using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Json.Services.JsonEffectFactory;

public class JsonEffectProviderFactory : IEffectProviderFactory
{
    public IEffectProvider Create() => new JsonEffect.JsonEffectProvider();
}
