using ChainSharp.Exceptions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp;

public abstract class Step<TIn, TOut> : IStep<TIn, TOut>
{
    public abstract Task<Either<WorkflowException, TOut>> Run(TIn input);
    
    public async Task<Either<WorkflowException, TOut>> RailwayStep(
        Either<WorkflowException, TIn> previousStep)
    {
        if (previousStep.IsLeft)
            return previousStep.Swap().ValueUnsafe();

        Console.WriteLine($"Running Step ({previousStep.GetUnderlyingRightType().Name})");
        return await Run(previousStep.ValueUnsafe());
    }
}