using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    /// <summary>
    /// Resolves the workflow with the provided return value.
    /// This method is used when you already have an Either result to return.
    /// </summary>
    /// <param name="returnType">The Either result to return, unless there's an exception</param>
    /// <returns>Either the provided result or the workflow's exception</returns>
    /// <remarks>
    /// If the workflow has an exception set, it will be returned instead of the provided result.
    /// This ensures that exceptions are properly propagated through the workflow.
    /// </remarks>
    public Either<Exception, TReturn> Resolve(Either<Exception, TReturn> returnType) =>
        Exception ?? returnType;

    /// <summary>
    /// Resolves the workflow by extracting the result from Memory.
    /// This is typically the last method called in a workflow's RunInternal implementation.
    /// </summary>
    /// <returns>Either the workflow's result or an exception</returns>
    /// <remarks>
    /// The resolution process follows this order of precedence:
    /// 1. If there's an exception, return it
    /// 2. If a short-circuit value is set, return it
    /// 3. Try to extract the result type from Memory
    /// 4. If the result type can't be found, return an exception
    /// </remarks>
    public Either<Exception, TReturn> Resolve()
    {
        // If there's an exception, return it
        if (Exception is not null)
            return Exception;

        // If a short-circuit value is set, return it
        if (ShortCircuitValueSet)
            return ShortCircuitValue;

        // Try to extract the result type from Memory
        var result = this.ExtractTypeFromMemory<TReturn, TInput, TReturn>();

        // If the result type can't be found, return an exception
        if (result is null)
            return new WorkflowException($"Could not find type: ({typeof(TReturn)}).");

        return (TReturn)result;
    }
}
