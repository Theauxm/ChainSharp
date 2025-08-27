using ChainSharp.Effect.Services.StepEffectProvider;

namespace ChainSharp.Effect.Services.StepEffectProviderFactory;

public interface IStepEffectProviderFactory
{
    IStepEffectProvider Create();
}
