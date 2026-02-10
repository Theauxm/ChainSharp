using ChainSharp.Effect.Mediator.Services.WorkflowRegistry;

namespace ChainSharp.Effect.Scheduler.Extensions;

/// <summary>
/// Extension methods for <see cref="IWorkflowRegistry"/> used by the scheduler.
/// </summary>
public static class WorkflowRegistryExtensions
{
    /// <summary>
    /// Validates that a workflow is registered for the specified input type.
    /// </summary>
    /// <typeparam name="TInput">The input type to validate</typeparam>
    /// <param name="registry">The workflow registry to check</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no workflow is registered for the input type.
    /// </exception>
    public static void ValidateWorkflowRegistration<TInput>(this IWorkflowRegistry registry)
    {
        var inputType = typeof(TInput);
        if (!registry.InputTypeToWorkflow.ContainsKey(inputType))
        {
            throw new InvalidOperationException(
                $"Workflow for input type '{inputType.Name}' is not registered in the WorkflowRegistry. "
                    + $"Ensure the workflow assembly is included in AddEffectWorkflowBus()."
            );
        }
    }
}
