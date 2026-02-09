using ChainSharp.Effect.Services.StepEffectProvider;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using ChainSharp.Effect.StepProvider.Logging.Services.StepLoggerProvider;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.StepProvider.Logging.Services.StepLoggerFactory;

public class StepLoggerFactory(IServiceProvider serviceProvider) : IStepEffectProviderFactory
{
    public IStepEffectProvider Create() =>
        serviceProvider.GetRequiredService<IStepLoggerProvider>();
}
