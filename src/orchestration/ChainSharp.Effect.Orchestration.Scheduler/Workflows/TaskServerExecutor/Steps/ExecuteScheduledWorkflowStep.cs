using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor.Steps;

/// <summary>
/// Executes the target workflow using the WorkflowBus with the resolved input.
/// </summary>
internal class ExecuteScheduledWorkflowStep(
    IWorkflowBus workflowBus,
    ILogger<ExecuteScheduledWorkflowStep> logger
) : EffectStep<(Metadata, ResolvedWorkflowInput), Unit>
{
    public override async Task<Unit> Run((Metadata, ResolvedWorkflowInput) input)
    {
        var (metadata, resolvedInput) = input;

        logger.LogDebug(
            "Executing workflow {WorkflowName} for Metadata {MetadataId}",
            metadata.Name,
            metadata.Id
        );

        await workflowBus.RunAsync(resolvedInput.Value, metadata);

        logger.LogDebug(
            "Successfully executed workflow {WorkflowName} for Metadata {MetadataId}",
            metadata.Name,
            metadata.Id
        );

        return Unit.Default;
    }
}
