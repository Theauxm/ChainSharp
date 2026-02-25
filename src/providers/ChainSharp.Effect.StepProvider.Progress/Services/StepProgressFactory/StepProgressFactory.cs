using ChainSharp.Effect.Services.StepEffectProvider;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using ChainSharp.Effect.StepProvider.Progress.Services.StepProgressProvider;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.StepProvider.Progress.Services.StepProgressFactory;

public class StepProgressFactory(IServiceProvider serviceProvider) : IStepEffectProviderFactory
{
    public IStepEffectProvider Create() =>
        serviceProvider.GetRequiredService<IStepProgressProvider>();
}
