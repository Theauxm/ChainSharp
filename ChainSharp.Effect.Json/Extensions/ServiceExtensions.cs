using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Json.Services.JsonEffectFactory;
using ChainSharp.Effect.Services.EffectProviderFactory;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Json.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddJsonEffect(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    ) => configurationBuilder.AddEffect<IEffectProviderFactory, JsonEffectProviderFactory>();
}
