using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Effects.ParameterEffect;

public interface IParameterEffectProviderFactory : IEffectProviderFactory
{
    public List<ParameterEffect> Providers { get; }
}