using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.EffectLogger;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddChainSharpEffects(
        this IServiceCollection serviceCollection,
        Action<ChainSharpEffectConfigurationBuilder>? options = null
    )
    {
        var configuration = BuildConfiguration(serviceCollection, options);

        return serviceCollection;
    }

    private static ChainSharpEffectConfiguration BuildConfiguration(
        IServiceCollection serviceCollection,
        Action<ChainSharpEffectConfigurationBuilder>? options
    )
    {
        // Create Builder to be used after Options are invoked
        var builder = new ChainSharpEffectConfigurationBuilder(serviceCollection);

        // Options able to be null since all values have defaults
        options?.Invoke(builder);

        return builder.Build();
    }

    public static ChainSharpEffectConfigurationBuilder AddConsoleLogger(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    ) => configurationBuilder.AddCustomLogger<EffectLogger>();

    public static ChainSharpEffectConfigurationBuilder AddCustomLogger<TWorkflowLogger>(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    )
        where TWorkflowLogger : class, IEffectLogger
    {
        configurationBuilder.ServiceCollection.AddScoped<IEffectLogger, TWorkflowLogger>();

        return configurationBuilder;
    }
}
