using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.EffectWorkflow;

namespace ChainSharp.Effect.Services.StepEffectProvider;

public interface IStepEffectProvider : IDisposable
{
    Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    );

    Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow,
        CancellationToken cancellationToken
    );
}
