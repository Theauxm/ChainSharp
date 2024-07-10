using LanguageExt;

namespace ChainSharp.Step;

public interface IStep<TIn, TOut>
{
    public Task<TOut> Run(TIn input);

    public Task<Either<Exception, TOut>> RailwayStep(
        Either<Exception, TIn> previousStep);
}