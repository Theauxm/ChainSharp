using System.Reflection;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Step;

public abstract class Step<TIn, TOut> : IStep<TIn, TOut>
{
    public abstract Task<TOut> Run(TIn input);

    public async Task<Either<Exception, TOut>> RailwayStep(Either<Exception, TIn> previousOutput)
    {
        if (previousOutput.IsLeft)
            return previousOutput.Swap().ValueUnsafe();

        var stepName = GetType().Name;

        try
        {
            return await Run(previousOutput.ValueUnsafe());
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
                    $"{{ \"step\": \"{stepName}\", \"type\": \"{e.GetType().Name}\", \"message\": \"{e.Message}\" }}"
                );

            return e;
        }
    }
}
