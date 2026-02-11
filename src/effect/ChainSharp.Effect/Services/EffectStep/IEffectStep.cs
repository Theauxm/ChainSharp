using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Effect.Services.EffectStep;

public interface IEffectStep<TIn, TOut> : IStep<TIn, TOut>
{
    public Task<Either<Exception, TOut>> RailwayStep<TWorkflowIn, TWorkflowOut>(
        Either<Exception, TIn> previousOutput,
        EffectWorkflow<TWorkflowIn, TWorkflowOut> effectWorkflow
    );
}
