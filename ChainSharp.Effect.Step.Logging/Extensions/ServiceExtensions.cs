using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Services.StepEffectProvider;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using ChainSharp.Effect.Step.Logging.Services.StepLoggerFactory;
using ChainSharp.Effect.Step.Logging.Services.StepLoggerProvider;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Step.Logging.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddStepLogger(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    )
    {
        configurationBuilder.ServiceCollection.AddTransient<
            IStepLoggerProvider,
            StepLoggerProvider
        >();

        return configurationBuilder.AddStepEffect<StepLoggerFactory>();
    }
}
