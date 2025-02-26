using ChainSharp.Effect.Services.Effect;
using ChainSharp.Effect.Services.EffectFactory;

namespace ChainSharp.Effect.Json.Services.JsonEffectFactory;

public class JsonEffectFactory : IEffectFactory
{
    public IEffect Create()
    {
        return new JsonEffect.JsonEffect();
    }
}
