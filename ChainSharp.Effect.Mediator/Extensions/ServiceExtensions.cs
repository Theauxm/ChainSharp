using System.Reflection;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Mediator.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection RegisterEffectWorkflows(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient,
        params Assembly[] assemblies
    )
    {
        var workflowType = typeof(IEffectWorkflow<,>);

        var types = new List<(Type, Type)>();
        foreach (var assembly in assemblies)
        {
            var workflowTypes = assembly
                .GetTypes()
                .Where(x => x.IsClass)
                .Where(
                    x =>
                        x.GetInterfaces()
                            .Where(y => y.IsGenericType)
                            .Select(y => y.GetGenericTypeDefinition())
                            .Contains(workflowType)
                )
                .Select(
                    type =>
                        (
                            type.GetInterfaces().FirstOrDefault(y => y.IsGenericType == false)
                                ?? type.GetInterfaces().FirstOrDefault()
                                ?? throw new WorkflowException(
                                    $"Could not find an interface attached to ({type.Name}). At least one Interface is required."
                                ),
                            type
                        )
                );

            types.AddRange(workflowTypes);
        }

        foreach (var (typeInterface, typeImplementation) in types)
        {
            switch (serviceLifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingletonChainSharpWorkflow(typeInterface, typeImplementation);
                    break;
                case ServiceLifetime.Scoped:
                    services.AddScopedChainSharpWorkflow(typeInterface, typeImplementation);
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransientChainSharpWorkflow(typeInterface, typeImplementation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(serviceLifetime),
                        serviceLifetime,
                        null
                    );
            }
        }

        return services;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffectWorkflowBus(
        this ChainSharpEffectConfigurationBuilder configurationBuilder,
        ServiceLifetime effectWorkflowServiceLifetime = ServiceLifetime.Transient,
        params Assembly[] assemblies
    )
    {
        configurationBuilder.ServiceCollection.AddEffectWorkflowBus(
            effectWorkflowServiceLifetime,
            assemblies
        );

        return configurationBuilder;
    }

    public static IServiceCollection AddEffectWorkflowBus(
        this IServiceCollection serviceCollection,
        ServiceLifetime effectWorkflowServiceLifetime = ServiceLifetime.Transient,
        params Assembly[] assemblies
    )
    {
        var workflowRegistry = new WorkflowRegistry(assemblies);

        return serviceCollection
            .AddSingleton<IWorkflowRegistry>(workflowRegistry)
            .AddSingleton<IWorkflowBus, WorkflowBus>()
            .RegisterEffectWorkflows(effectWorkflowServiceLifetime, assemblies);
    }
}
