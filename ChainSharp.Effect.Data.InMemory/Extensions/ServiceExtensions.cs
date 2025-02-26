using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Data.InMemory.Services.InMemoryContextFactory;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Extensions;

namespace ChainSharp.Effect.Data.InMemory.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder AddInMemoryEffect(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    ) => configurationBuilder.AddEffect<IDataContextFactory, InMemoryContextFactory>();
}
