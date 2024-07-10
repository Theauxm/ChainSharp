using ChainSharp.Exceptions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    public Either<Exception, TReturn> Resolve(Either<Exception, TReturn> returnType) =>
        Exception ?? returnType;

    public Either<Exception, TReturn> Resolve()
    {
        if (Exception is not null)
            return Exception;

        if (ShortCircuitValue is not null)
            return ShortCircuitValue;

        var result = Memory.GetValueOrDefault(typeof(TReturn));

        if (result is null)
            return new WorkflowException($"Could not find type: ({typeof(TReturn)}).");

        return (TReturn)result;
    }
}
