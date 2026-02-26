using ChainSharp.Effect.Services.ServiceTrain;
using ChainSharp.Step;
using LanguageExt;

namespace ChainSharp.Effect.Services.EffectStep;

public interface IEffectStep<TIn, TOut> : IStep<TIn, TOut>
{
    public Task<Either<Exception, TOut>> RailwayStep<TTrainIn, TTrainOut>(
        Either<Exception, TIn> previousOutput,
        ServiceTrain<TTrainIn, TTrainOut> serviceTrain
    );
}
