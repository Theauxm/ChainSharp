using ChainSharp.Exceptions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp;

public abstract class Step<TIn, TOut> : IStep<TIn, TOut>
{
    public abstract Task<TOut> Run(TIn input);
    
    public async Task<Either<Exception, TOut>> RailwayStep(
        Either<Exception, TIn> previousStep)
    {
        if (previousStep.IsLeft)
            return previousStep.Swap().ValueUnsafe();

        Console.WriteLine($"Running Step ({previousStep.GetUnderlyingRightType().Name})");

        try
        {
            return await Run(previousStep.ValueUnsafe());
        }
        catch (Exception e)
        {
            Console.WriteLine($"Found Exception ({e})");
            return e;
        }
    }
}