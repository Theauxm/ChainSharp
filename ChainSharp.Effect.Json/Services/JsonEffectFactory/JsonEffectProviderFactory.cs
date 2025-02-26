using ChainSharp.Effect.Services.EffectLogger;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Json.Services.JsonEffectFactory;

public class JsonEffectProviderFactory(IEffectLogger effectLogger) : IEffectProviderFactory
{
    public IEffectProvider Create() => new JsonEffect.JsonEffectProvider(effectLogger);
}
