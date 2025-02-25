using ChainSharp.Logging.Configuration.ChainSharpLoggingBuilder;
using ChainSharp.Logging.Configuration.ChainSharpLoggingConfiguration;
using ChainSharp.Logging.Services.WorkflowLogger;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Logging.Extensions;

public static class ServicesExtensions
{
    public static IServiceCollection AddChainSharpLogging(
        this IServiceCollection serviceCollection,
        Action<ChainSharpLoggingBuilder>? options = null
    )
    {
        var configuration = BuildConfiguration(serviceCollection, options);

        return serviceCollection;
    }

    private static ChainSharpLoggingConfiguration BuildConfiguration(
        IServiceCollection serviceCollection,
        Action<ChainSharpLoggingBuilder>? options
    )
    {
        // Create Builder to be used after Options are invoked
        var builder = new ChainSharpLoggingBuilder(serviceCollection);

        // Options able to be null since all values have defaults
        options?.Invoke(builder);

        return builder.Build();
    }

    public static ChainSharpLoggingBuilder AddConsoleLogger(
        this ChainSharpLoggingBuilder builder
    ) => builder.AddCustomLogger<WorkflowLogger>();

    public static ChainSharpLoggingBuilder AddCustomLogger<TWorkflowLogger>(
        this ChainSharpLoggingBuilder builder
    )
        where TWorkflowLogger : class, IWorkflowLogger
    {
        builder.ServiceCollection.AddScoped<IWorkflowLogger, TWorkflowLogger>();

        return builder;
    }
}
