using ChainSharp.Logging.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Logging.Configuration.ChainSharpLoggingConfiguration;
using ChainSharp.Logging.Services.WorkflowLogger;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Logging.Extensions;

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
    ) => configurationBuilder.AddCustomLogger<WorkflowLogger>();

    public static ChainSharpLoggingConfigurationBuilder AddCustomLogger<TWorkflowLogger>(
        this ChainSharpLoggingConfigurationBuilder configurationBuilder
    )
        where TWorkflowLogger : class, IWorkflowLogger
    {
        configurationBuilder.ServiceCollection.AddScoped<IWorkflowLogger, TWorkflowLogger>();

        return configurationBuilder;
    }
}
