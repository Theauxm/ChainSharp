namespace ChainSharp.Workflow;

/// <summary>
/// Defines the contract for a workflow.
/// A workflow is a sequence of steps that processes an input and produces a result.
/// </summary>
/// <typeparam name="TInput">The type of input the workflow accepts</typeparam>
/// <typeparam name="TReturn">The type of result the workflow produces</typeparam>
public interface IWorkflow<in TInput, TReturn>
{
    /// <summary>
    /// Executes the workflow with the provided input.
    /// This method orchestrates the execution of all steps in the workflow.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>The result produced by the workflow</returns>
    /// <remarks>
    /// If any step in the workflow throws an exception, it will be propagated to the caller.
    /// For error handling with the Railway pattern, use the RunEither method in the implementation.
    /// </remarks>
    public Task<TReturn> Run(TInput input);
}
