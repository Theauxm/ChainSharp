using ChainSharp.Effect.Services.Effect;

namespace ChainSharp.Effect.Services.EffectFactory;

public interface IEffectFactory
{
    public IEffect Create();
}
