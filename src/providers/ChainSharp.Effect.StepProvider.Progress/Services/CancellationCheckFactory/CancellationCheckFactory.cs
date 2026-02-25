using ChainSharp.Effect.Services.StepEffectProvider;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using ChainSharp.Effect.StepProvider.Progress.Services.CancellationCheckProvider;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.StepProvider.Progress.Services.CancellationCheckFactory;

public class CancellationCheckFactory(IServiceProvider serviceProvider) : IStepEffectProviderFactory
{
    public IStepEffectProvider Create() =>
        serviceProvider.GetRequiredService<ICancellationCheckProvider>();
}
