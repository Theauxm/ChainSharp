using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;

namespace ChainSharp.Effect.StepProvider.Progress.Services.StepProgressProvider;

public class StepProgressProvider : IStepProgressProvider
{
    public async Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        if (effectWorkflow.Metadata is null || effectWorkflow.EffectRunner is null)
            return;

        effectWorkflow.Metadata.CurrentlyRunningStep = effectStep.Metadata?.Name;
        effectWorkflow.Metadata.StepStartedAt = DateTime.UtcNow;

        await effectWorkflow.EffectRunner.Update(effectWorkflow.Metadata);
        await effectWorkflow.EffectRunner.SaveChanges(cancellationToken);
    }

    public async Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    )
    {
        if (effectWorkflow.Metadata is null || effectWorkflow.EffectRunner is null)
            return;

        effectWorkflow.Metadata.CurrentlyRunningStep = null;
        effectWorkflow.Metadata.StepStartedAt = null;

        await effectWorkflow.EffectRunner.Update(effectWorkflow.Metadata);
        await effectWorkflow.EffectRunner.SaveChanges(cancellationToken);
    }

    public void Dispose() { }
}
