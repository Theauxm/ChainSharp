using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Effect.Services.ServiceTrain;

namespace ChainSharp.Effect.Services.StepEffectProvider;

public interface IStepEffectProvider : IDisposable
{
    Task BeforeStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        ServiceTrain<TWorkflowIn, TWorkflowOut> serviceTrain,
        CancellationToken cancellationToken
    );

    Task AfterStepExecution<TIn, TOut, TWorkflowIn, TWorkflowOut>(
        EffectStep<TIn, TOut> effectStep,
        ServiceTrain<TWorkflowIn, TWorkflowOut> serviceTrain,
        CancellationToken cancellationToken
    );
}
