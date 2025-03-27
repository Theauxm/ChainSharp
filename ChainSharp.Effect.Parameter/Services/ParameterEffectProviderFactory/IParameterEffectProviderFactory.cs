using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Parameter.Services.ParameterEffectProviderFactory;

public interface IParameterEffectProviderFactory : IEffectProviderFactory
{
    public List<ParameterEffect> Providers { get; }
}
