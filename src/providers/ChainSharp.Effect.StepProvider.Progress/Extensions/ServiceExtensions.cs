using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.StepProvider.Progress.Services.CancellationCheckFactory;
using ChainSharp.Effect.StepProvider.Progress.Services.CancellationCheckProvider;
using ChainSharp.Effect.StepProvider.Progress.Services.StepProgressFactory;
using ChainSharp.Effect.StepProvider.Progress.Services.StepProgressProvider;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.StepProvider.Progress.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddStepProgress(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    )
    {
        configurationBuilder.ServiceCollection.AddTransient<
            ICancellationCheckProvider,
            CancellationCheckProvider
        >();
        configurationBuilder.ServiceCollection.AddTransient<
            IStepProgressProvider,
            StepProgressProvider
        >();

        // Register CancellationCheck FIRST so it runs before StepProgress sets columns
        return configurationBuilder
            .AddStepEffect<CancellationCheckFactory>()
            .AddStepEffect<StepProgressFactory>();
    }
}
