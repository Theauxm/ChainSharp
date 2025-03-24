using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.EffectProvider;

namespace ChainSharp.Effect.Effects.ParameterEffect;

public class ParameterEffectProviderFactory(IChainSharpEffectConfiguration configuration) : IParameterEffectProviderFactory
{
    public List<ParameterEffect> Providers { get; } = [];
    public IEffectProvider Create()
    {
        var parameterEffect = new ParameterEffect(configuration.WorkflowParameterJsonSerializerOptions);
        
        Providers.Add(parameterEffect);
        
        return parameterEffect;
    }

}