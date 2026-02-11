using ChainSharp.Effect.Services.EffectWorkflow;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;

public class WorkflowDiscoveryService : IWorkflowDiscoveryService
{
    private readonly IServiceCollection _serviceCollection;
    private IReadOnlyList<WorkflowRegistration>? _cachedRegistrations;

    public WorkflowDiscoveryService(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;
    }

    public IReadOnlyList<WorkflowRegistration> DiscoverWorkflows()
    {
        if (_cachedRegistrations != null)
            return _cachedRegistrations;

        var effectWorkflowType = typeof(IEffectWorkflow<,>);
        var registrations = new List<WorkflowRegistration>();

        foreach (var descriptor in _serviceCollection)
        {
            var serviceType = descriptor.ServiceType;

            // Find IEffectWorkflow<,> either directly or via interface hierarchy
            Type? effectInterface = FindEffectWorkflowInterface(serviceType, effectWorkflowType);

            if (effectInterface == null)
                continue;

            // Skip concrete type registrations from the dual-registration pattern
            // (AddScopedChainSharpWorkflow registers both TImplementation and TService)
            if (descriptor.ImplementationFactory != null && !serviceType.IsInterface)
                continue;

            var genericArgs = effectInterface.GetGenericArguments();
            var inputType = genericArgs[0];
            var outputType = genericArgs[1];

            var implementationType =
                descriptor.ImplementationType
                ?? descriptor.ImplementationInstance?.GetType()
                ?? descriptor.ServiceType;

            registrations.Add(
                new WorkflowRegistration
                {
                    ServiceType = serviceType,
                    ImplementationType = implementationType,
                    InputType = inputType,
                    OutputType = outputType,
                    Lifetime = descriptor.Lifetime,
                    ServiceTypeName = GetFriendlyTypeName(serviceType),
                    ImplementationTypeName = GetFriendlyTypeName(implementationType),
                    InputTypeName = GetFriendlyTypeName(inputType),
                    OutputTypeName = GetFriendlyTypeName(outputType),
                }
            );
        }

        // Deduplicate: prefer interface registrations over concrete type registrations
        _cachedRegistrations = registrations
            .GroupBy(r => r.ImplementationType)
            .Select(g => g.FirstOrDefault(r => r.ServiceType.IsInterface) ?? g.First())
            .ToList()
            .AsReadOnly();

        return _cachedRegistrations;
    }

    private static Type? FindEffectWorkflowInterface(Type type, Type effectWorkflowType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == effectWorkflowType)
            return type;

        return type.GetInterfaces()
            .FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == effectWorkflowType
            );
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
        return $"{name}<{args}>";
    }
}
