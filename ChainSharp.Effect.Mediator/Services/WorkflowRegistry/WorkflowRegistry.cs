using System.Reflection;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;

namespace ChainSharp.Effect.Mediator.Services.WorkflowRegistry;

public class WorkflowRegistry : IWorkflowRegistry
{
    public Dictionary<Type, Type> InputTypeToWorkflow { get; set; }

    public WorkflowRegistry(params Assembly[] assemblies)
    {
        // The type we will be looking for in our assemblies
        var workflowType = typeof(IEffectWorkflow<,>);

        var allWorkflowTypes = new HashSet<Type>();

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
                    x =>
                        // Prefer to inject via interface, but if it doesn't exist then inject by underlying type
                        x.GetInterfaces().FirstOrDefault(y => y.IsGenericType == false) ?? x
                );

            allWorkflowTypes.UnionWith(workflowTypes);
        }

        InputTypeToWorkflow = allWorkflowTypes.ToDictionary(
            x =>
                x.GetInterfaces()
                    .Where(interfaceType => interfaceType.IsGenericType)
                    .FirstOrDefault(
                        interfaceType => interfaceType.GetGenericTypeDefinition() == workflowType
                    )
                    ?.GetGenericArguments()
                    .FirstOrDefault()
                ?? throw new WorkflowException(
                    $"Could not find an interface and/or an inherited interface of type ({workflowType.Name}) on target type ({x.Name}) with FullName ({x.FullName}) on Assembly ({x.AssemblyQualifiedName})."
                )
        );
    }
}
