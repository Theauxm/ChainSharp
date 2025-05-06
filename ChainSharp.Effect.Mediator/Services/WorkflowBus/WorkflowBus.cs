using System.Reflection;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using LanguageExt.ClassInstances;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Mediator.Services.WorkflowBus;

/// <summary>
/// Implements the workflow bus that dynamically executes workflows based on their input type.
/// </summary>
/// <remarks>
/// The WorkflowBus class provides the core implementation of the mediator pattern for workflows.
/// It uses reflection and dependency injection to dynamically discover, instantiate, and execute
/// the appropriate workflow for a given input type.
///
/// The workflow bus relies on the workflow registry to map input types to workflow types,
/// and uses the service provider to resolve and instantiate the workflow instances.
///
/// This implementation supports:
/// - Dynamic workflow discovery based on input type
/// - Automatic dependency injection for workflow instances
/// - Property injection for workflows
/// - Metadata association for tracking and logging
/// - Type-safe execution with generic output types
///
/// The workflow bus is typically registered as a scoped service in the dependency injection
/// container, allowing it to be injected into controllers, services, or other components
/// that need to execute workflows.
/// </remarks>
/// <param name="serviceProvider">The service provider used to resolve workflow instances</param>
/// <param name="registryService">The registry service that maps input types to workflow types</param>
public class WorkflowBus(IServiceProvider serviceProvider, IWorkflowRegistry registryService)
    : IWorkflowBus
{
    /// <summary>
    /// Executes a workflow that accepts the specified input type and returns the specified output type.
    /// </summary>
    /// <typeparam name="TOut">The expected output type of the workflow</typeparam>
    /// <param name="workflowInput">The input object for the workflow</param>
    /// <param name="metadata">Optional metadata to associate with the workflow execution</param>
    /// <returns>A task that resolves to the workflow's output</returns>
    /// <exception cref="WorkflowException">
    /// Thrown when the input is null, no workflow is found for the input type,
    /// the Run method cannot be found on the workflow, or the Run method invocation fails.
    /// </exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Validates that the input is not null
    /// 2. Gets the type of the input object
    /// 3. Looks up the workflow type in the registry based on the input type
    /// 4. Resolves the workflow instance from the service provider
    /// 5. Injects properties into the workflow instance
    /// 6. Sets the parent ID if metadata is provided
    /// 7. Finds the appropriate Run method on the workflow using reflection
    /// 8. Invokes the Run method with the input
    /// 9. Returns the result cast to the expected output type
    ///
    /// The method uses reflection to find and invoke the Run method because workflows
    /// can have multiple Run method implementations, and we need to select the correct one
    /// from ChainSharp.Effect rather than the base ChainSharp implementation.
    /// </remarks>
    public Task<TOut> RunAsync<TOut>(object workflowInput, Metadata? metadata = null)
    {
        if (workflowInput == null)
            throw new WorkflowException(
                "workflowInput is null as input to WorkflowBus.SendAsync(...)"
            );

        // The full type of the input, rather than just the interface
        var inputType = workflowInput.GetType();

        var foundWorkflow = registryService.InputTypeToWorkflow.TryGetValue(
            inputType,
            out var correctWorkflow
        );

        if (foundWorkflow == false || correctWorkflow == null)
            throw new WorkflowException(
                $"Could not find workflow with input type ({inputType.Name})"
            );

        var workflowService = serviceProvider.GetRequiredService(correctWorkflow);
        serviceProvider.InjectProperties(workflowService);

        if (metadata != null)
        {
            var parentIdProperty = workflowService
                .GetType()
                .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .First(x => x.Name == "ParentId");

            parentIdProperty.SetValue(workflowService, metadata.Id);
        }

        // Get the run methodInfo from the workflow type
        var runMethod = workflowService
            .GetType()
            .GetMethods()
            // Run function on Workflow
            .Where(x => x.Name == "Run")
            // Run(input) and Run(input, serviceCollection) exist. We want the former.
            .Where(x => x.GetParameters().Length == 1)
            // Run(input) has an implementation in ChainSharp and ChainSharp.Effect (the latter with the "new" keyword).
            // We want the one from ChainSharp.Effect
            .First(x => x.Module.Name.Contains("Effect"));

        // Make sure the Run MethodInfo was properly found
        if (runMethod is null)
            throw new WorkflowException(
                $"Failed to find Run method for workflow type ({workflowService.GetType().Name})"
            );

        // And finally run the workflow, casting the return type to preserve type safety.
        var taskRunMethod = (Task<TOut>?)runMethod.Invoke(workflowService, [workflowInput]);

        if (taskRunMethod is null)
            throw new WorkflowException(
                $"Failed to invoke Run method for workflow type ({workflowService.GetType().Name})"
            );

        return taskRunMethod;
    }
}
