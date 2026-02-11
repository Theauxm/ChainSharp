using ChainSharp.Extensions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Workflow;

/// <summary>
/// Base class for all workflows in the ChainSharp system.
/// A workflow is a sequence of steps that processes an input and produces a result.
/// This class provides the core functionality for workflow execution, including
/// Railway-oriented programming support, memory management, and service integration.
/// </summary>
/// <typeparam name="TInput">The type of input the workflow accepts</typeparam>
/// <typeparam name="TReturn">The type of result the workflow produces</typeparam>
public abstract partial class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    /// <summary>
    /// Gets or sets the exception that occurred during workflow execution.
    /// If set, this exception will be returned by Resolve() and will short-circuit further execution.
    /// </summary>
    protected internal Exception? Exception { get; set; }

    /// <summary>
    /// The memory dictionary that stores all objects available to the workflow.
    /// This includes inputs, outputs of steps, and services.
    /// Objects are stored by their Type, allowing for type-based retrieval.
    /// </summary>
    protected internal Dictionary<Type, object> Memory { get; private set; } = null!;

    /// <summary>
    /// The value to return when short-circuiting the workflow.
    /// This is set by the ShortCircuit method.
    /// </summary>
    private TReturn ShortCircuitValue { get; set; } = default!;

    public string ExternalId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Indicates whether a short-circuit value has been set.
    /// </summary>
    private bool ShortCircuitValueSet { get; set; } = false;

    /// <summary>
    /// Executes the workflow with the provided input.
    /// This method unwraps the Either result from RunEither and throws any exceptions.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>The result produced by the workflow</returns>
    /// <exception cref="Exception">Thrown if any step in the workflow fails</exception>
    public async Task<TReturn> Run(TInput input)
    {
        var resultEither = await RunEither(input);

        if (resultEither.IsLeft)
            resultEither.Swap().ValueUnsafe().Rethrow();

        return resultEither.Unwrap();
    }

    /// <summary>
    /// Executes the workflow with the provided input and service provider.
    /// This method unwraps the Either result from RunEither and throws any exceptions.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <returns>The result produced by the workflow</returns>
    /// <exception cref="Exception">Thrown if any step in the workflow fails</exception>
    public async Task<TReturn> Run(TInput input, IServiceProvider serviceProvider)
    {
        var resultEither = await RunEither(input, serviceProvider);

        if (resultEither.IsLeft)
            resultEither.Swap().ValueUnsafe().Rethrow();

        return resultEither.Unwrap();
    }

    /// <summary>
    /// Executes the workflow with Railway-oriented programming support.
    /// This method initializes the Memory dictionary and calls RunInternal.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>Either the result of the workflow or an exception</returns>
    public Task<Either<Exception, TReturn>> RunEither(TInput input)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object> { { typeof(Unit), Unit.Default } };

        return RunInternal(input);
    }

    /// <summary>
    /// Executes the workflow with Railway-oriented programming support and service provider.
    /// This method initializes the Memory dictionary with the service provider and calls RunInternal.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <returns>Either the result of the workflow or an exception</returns>
    public Task<Either<Exception, TReturn>> RunEither(
        TInput input,
        IServiceProvider serviceProvider
    )
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>
        {
            { typeof(Unit), Unit.Default },
            { typeof(IServiceProvider), serviceProvider }
        };

        return RunInternal(input);
    }

    /// <summary>
    /// The core implementation method that executes the workflow.
    /// This must be implemented by derived classes to define the workflow's behavior.
    /// </summary>
    /// <param name="input">The input data for the workflow</param>
    /// <returns>Either the result of the workflow or an exception</returns>
    protected abstract Task<Either<Exception, TReturn>> RunInternal(TInput input);
}
