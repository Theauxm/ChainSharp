using ChainSharp.Effect.Data.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Effect.Data.Configuration.ChainSharpLoggingConfiguration;
using ChainSharp.Effect.Data.Services.EffectLogger;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Data.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddChainSharpLogging(
        this IServiceCollection serviceCollection,
        Action<ChainSharpLoggingConfigurationBuilder>? options = null
    )
    {
        var configuration = BuildConfiguration(serviceCollection, options);

        return serviceCollection;
    }

    private static ChainSharpLoggingConfiguration BuildConfiguration(
        IServiceCollection serviceCollection,
        Action<ChainSharpLoggingConfigurationBuilder>? options
    )
    {
        // Create Builder to be used after Options are invoked
        var builder = new ChainSharpLoggingConfigurationBuilder(serviceCollection);

        // Options able to be null since all values have defaults
        options?.Invoke(builder);

        return builder.Build();
    }

    public static ChainSharpLoggingConfigurationBuilder AddConsoleLogger(
        this ChainSharpLoggingConfigurationBuilder configurationBuilder
    ) => configurationBuilder.AddCustomLogger<EffectLogger>();

    public static ChainSharpLoggingConfigurationBuilder AddCustomLogger<TWorkflowLogger>(
        this ChainSharpLoggingConfigurationBuilder configurationBuilder
    )
        where TWorkflowLogger : class, IEffectLogger
    {
        configurationBuilder.ServiceCollection.AddScoped<IEffectLogger, TWorkflowLogger>();

        return configurationBuilder;
    }
}
