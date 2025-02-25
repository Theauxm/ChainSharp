using ChainSharp.Logging.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Logging.InMemory.Services.InMemoryContext;
using ChainSharp.Logging.InMemory.Services.InMemoryContextFactory;
using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Logging.InMemory.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpLoggingConfigurationBuilder UseInMemoryProvider(
        this ChainSharpLoggingConfigurationBuilder configurationBuilder
    )
    {
        var inMemoryContextFactory = new InMemoryContextFactory();

        configurationBuilder
            .ServiceCollection.AddSingleton<ILoggingProviderContextFactory>(inMemoryContextFactory)
            .AddDbContext<ILoggingProviderContext, InMemoryContext>();

        return configurationBuilder;
    }
}
