using ChainSharp.Effect.Data.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Effect.Data.InMemory.Services.InMemoryContext;
using ChainSharp.Effect.Data.InMemory.Services.InMemoryContextFactory;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Data.InMemory.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpLoggingConfigurationBuilder UseInMemoryProvider(
        this ChainSharpLoggingConfigurationBuilder configurationBuilder
    )
    {
        var inMemoryContextFactory = new InMemoryContextFactory();

        configurationBuilder
            .ServiceCollection.AddSingleton<IDataContextFactory>(inMemoryContextFactory)
            .AddDbContext<IDataContext, InMemoryContext>();

        return configurationBuilder;
    }
}
