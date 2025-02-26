using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Log.Services.EffectLogger;
using ChainSharp.Effect.Services.EffectLogger;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Log.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddEffectLogger(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    )
    {
        configurationBuilder
            .ServiceCollection.AddLogging()
            .AddSingleton<IEffectLogger, EffectLogger>();

        return configurationBuilder.AddCustomLogger<EffectLogger>();
    }
}
