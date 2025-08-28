using System.Text.Json;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Step.Logging.Services.StepLoggerProvider;

public class StepLoggerProvider(IChainSharpEffectConfiguration configuration) : IStepLoggerProvider
{
    public async Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        var stepType = effectStep.GetType().Name;

        var workflowType = effectWorkflow.GetType().Name;

        effectWorkflow.EffectLogger?.LogTrace(
            "BEFORE STEP EXECUTION Success: ({Success}) Step Type: ({StepType}) Input Type: ({TypeIn}) Output Type: ({TypeOut}) Workflow: ({Workflow}) ExternalId ({ExternalId})",
            effectStep.PreviousResult.IsRight,
            stepType,
            typeof(TIn),
            typeof(TIn),
            workflowType,
            effectWorkflow.ExternalId
        );

        effectStep.PreviousResult.Match(
            Right: resultIn =>
            {
                if (resultIn is null)
                    return;

                var indentedJson = JsonSerializer.Serialize(
                    resultIn,
                    resultIn.GetType(),
                    configuration.WorkflowParameterJsonSerializerOptions
                );

                effectWorkflow.EffectLogger?.LogDebug(
                    "BEFORE STEP EXECUTION Workflow: ({WorkflowName}) ExternalId: ({ExternalId}) Step: ({StepName}) Input Type: ({InputType}) Input: ({Input})",
                    workflowType,
                    effectWorkflow.ExternalId,
                    stepType,
                    effectStep.PreviousResult.GetUnderlyingRightType(),
                    indentedJson
                );
            },
            Left: _ => { },
            Bottom: () => { }
        );
    }

    public async Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        var stepType = effectStep.GetType().Name;

        var workflowType = effectWorkflow.GetType().Name;

        effectWorkflow.EffectLogger?.LogTrace(
            "AFTER STEP EXECUTION Success ({Success}) Step Type: ({StepType}) Workflow: ({Workflow}) ExternalId ({ExternalId})",
            effectStep.Result.IsRight,
            stepType,
            workflowType,
            effectWorkflow.ExternalId
        );

        effectStep.Result.Match(
            Right: resultOut =>
            {
                if (resultOut is null)
                    return;

                var json = JsonSerializer.Serialize(
                    resultOut,
                    resultOut.GetType(),
                    configuration.WorkflowParameterJsonSerializerOptions
                );

                effectWorkflow.EffectLogger?.LogDebug(
                    "AFTER STEP EXECUTION Workflow: ({WorkflowName}) ExternalId: ({ExternalId}) Step: ({StepName}) Result Type: ({ResultType}) Result: ({Result})",
                    workflowType,
                    effectWorkflow.ExternalId,
                    stepType,
                    resultOut.GetType(),
                    json
                );
            },
            Left: _ =>
            {
                if (effectStep.ExceptionData is not null)
                    effectWorkflow.EffectLogger?.LogError(
                        "AFTER STEP EXECUTION Exception: ({ExceptionType}) Workflow: ({WorkflowName}) ExternalId: ({ExternalId}) Step: ({StepName}) Message: ({Message}).",
                        effectStep.ExceptionData.Type,
                        effectStep.ExceptionData.WorkflowName,
                        effectStep.ExceptionData.WorkflowExternalId,
                        effectStep.ExceptionData.Step,
                        effectStep.ExceptionData.Message
                    );
            },
            Bottom: () => { }
        );
    }

    public void Dispose() { }
}
