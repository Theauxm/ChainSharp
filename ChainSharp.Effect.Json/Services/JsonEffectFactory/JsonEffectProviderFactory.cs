using ChainSharp.Effect.Json.Services.JsonEffect;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Json.Services.JsonEffectFactory;

public class JsonEffectProviderFactory(IServiceProvider serviceProvider) : IEffectProviderFactory
{
    // public IEffectProvider Create() => new JsonEffectProvider(loggerFactory.CreateLogger<JsonEffectProvider>());
    public IEffectProvider Create() => serviceProvider.GetRequiredService<IJsonEffectProvider>();
}
