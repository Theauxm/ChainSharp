using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestExecutor.Steps;

/// <summary>
/// Executes the scheduled workflow using the WorkflowBus.
/// </summary>
internal class ExecuteScheduledWorkflowStep(
    IWorkflowBus workflowBus,
    ILogger<ExecuteScheduledWorkflowStep> logger
) : EffectStep<Metadata, Unit>
{
    public override async Task<Unit> Run(Metadata input)
    {
        var inputType = input.Manifest!.PropertyType;
        var workflowInput = input.Manifest.GetProperties(inputType);

        logger.LogDebug(
            "Executing workflow {WorkflowName} for Metadata {MetadataId}",
            input.Name,
            input.Id
        );

        await workflowBus.RunAsync(workflowInput, input);

        logger.LogDebug(
            "Successfully executed workflow {WorkflowName} for Metadata {MetadataId}",
            input.Name,
            input.Id
        );

        return Unit.Default;
    }
}
