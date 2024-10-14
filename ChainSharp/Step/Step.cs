using System.Reflection;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Step;

public abstract class Step<TIn, TOut> : IStep<TIn, TOut>
{
    public abstract Task<TOut> Run(TIn input);

    public async Task<Either<Exception, TOut>> RailwayStep(Either<Exception, TIn> previousStep)
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
            var messageField = typeof(Exception).GetField(
                "_message",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (messageField != null)
                messageField.SetValue(
                    e,
                    $"{{ \"Step\": \"{GetType().Name}\", \"Type\": \"{e.GetType().Name}\", \"Message\": \"{e.Message}\" }}"
                );

            Console.WriteLine($"Step: ({GetType().Name}) failed with Exception: ({e.Message})");
            return e;
        }
    }
}
