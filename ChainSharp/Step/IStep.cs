using LanguageExt;

namespace ChainSharp.Step;

/// <summary>
/// Defines the contract for a step in a workflow.
/// Steps are the fundamental building blocks of workflows, each performing a single operation
/// and returning either a result or an exception.
/// </summary>
/// <typeparam name="TIn">The input type for this step</typeparam>
/// <typeparam name="TOut">The output type produced by this step</typeparam>
public interface IStep<TIn, TOut>
{
    /// <summary>
    /// Executes the step's operation with the provided input.
    /// This is the core implementation method that derived classes must implement.
    /// </summary>
    /// <param name="input">The input data for this step</param>
    /// <returns>The output produced by this step</returns>
    public Task<TOut> Run(TIn input);

    /// <summary>
    /// Railway-oriented implementation that handles the Either monad pattern.
    /// This method enables the Railway-oriented programming pattern where operations
    /// can be chained together with automatic error handling.
    /// </summary>
    /// <param name="previousOutput">Either a result from the previous step or an exception</param>
    /// <returns>Either the result of this step or an exception</returns>
    public Task<Either<Exception, TOut>> RailwayStep(Either<Exception, TIn> previousOutput);
}
