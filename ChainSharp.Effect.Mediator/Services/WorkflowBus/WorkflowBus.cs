using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using LanguageExt.ClassInstances;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Mediator.Services.WorkflowBus;

public class WorkflowBus(IServiceProvider serviceProvider, IWorkflowRegistry registryService)
    : IWorkflowBus
{
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
            var metadataProperty = workflowService
                .GetType()
                .GetProperties()
                .First(x => x.Name == "Metadata");
            
            var metadataValue = metadataProperty.GetValue(workflowService);
            
            var parentIdProperty = metadataProperty
                .GetType()
                .GetProperties()
                .First(x => x.Name == "ParentId");
            
            parentIdProperty.SetValue(metadataValue, metadata.Id);
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
