using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.StepProvider.Logging.Services.StepLoggerFactory;
using ChainSharp.Effect.StepProvider.Logging.Services.StepLoggerProvider;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.StepProvider.Logging.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddStepLogger(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
        bool serializeStepData = false
    )
    {
        configurationBuilder.SerializeStepData = serializeStepData;
        configurationBuilder.ServiceCollection.AddTransient<
            IStepLoggerProvider,
            StepLoggerProvider
        >();

        return configurationBuilder.AddStepEffect<StepLoggerFactory>();
    }
}
