using ChainSharp.Exceptions;
using LanguageExt;

namespace ChainSharp;

public interface IStep<TIn, TOut>
{
    public Task<TOut> Run(TIn input);

    public Task<Either<WorkflowException, TOut>> RailwayStep(
        Either<WorkflowException, TIn> previousStep);
}