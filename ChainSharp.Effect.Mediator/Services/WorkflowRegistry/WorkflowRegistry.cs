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
                        x.GetInterfaces().FirstOrDefault(y => y.IsGenericType == false)
                        ?? throw new WorkflowException(
                            $"Could not find an interface with generic arguments on ({x.Name})."
                        )
                );

            allWorkflowTypes.UnionWith(workflowTypes);
        }

        InputTypeToWorkflow = allWorkflowTypes.ToDictionary(
            x =>
                x.GetInterfaces()
                    .Where(interfaceType => interfaceType.IsGenericType)
                    .First(
                        interfaceType => interfaceType.GetGenericTypeDefinition() == workflowType
                    )
                    .GetGenericArguments()
                    .FirstOrDefault()
                ?? throw new WorkflowException($"Could not find an interface on ({x.Name}).")
        );
    }
}
