using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Step;
using ChainSharp.Utils;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    #region Chain<TStep, TIn, TOut>

    /// <summary>
    /// Executes a step with the provided input and captures the output.
    /// This is the core Chain method that all other Chain methods ultimately call.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <typeparam name="TOut">The output type from the step</typeparam>
    /// <param name="step">The step instance to execute</param>
    /// <param name="previousStep">The input for the step</param>
    /// <param name="outVar">The output from the step</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Short-circuits if there's already an exception
    /// 2. Executes the step with the provided input
    /// 3. If the step fails (returns Left), sets the exception
    /// 4. If the step succeeds (returns Right), stores the result in Memory
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(
        TStep step,
        Either<Exception, TIn> previousStep,
        out Either<Exception, TOut> outVar
    )
        where TStep : IStep<TIn, TOut>
    {
        // If there's already an exception, short-circuit
        if (Exception is not null)
        {
            outVar = Exception;
            return this;
        }

        // Execute the step
        outVar = Task.Run(() => step.RailwayStep(previousStep, this)).Result;

        // Handle the result
        if (outVar.IsLeft)
            Exception ??= outVar.Swap().ValueUnsafe();
        else
        {
            var outValue = outVar.Unwrap()!;

            // Store the result in Memory
            if (typeof(TOut).IsTuple())
                this.AddTupleToMemory(outValue);
            else
                Memory[typeof(TOut)] = outValue;
        }

        return this;
    }

    /// <summary>
    /// Executes a step with input extracted from Memory.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <typeparam name="TOut">The output type from the step</typeparam>
    /// <param name="step">The step instance to execute</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Extracts the input from Memory
    /// 2. If the input is not found, short-circuits
    /// 3. Otherwise, executes the step with the extracted input
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step)
        where TStep : IStep<TIn, TOut>
    {
        // Extract the input from Memory
        var input = this.ExtractTypeFromMemory<TIn, TInput, TReturn>();

        if (input is null)
            return this;

        return Chain<TStep, TIn, TOut>(step, input, out var x);
    }

    /// <summary>
    /// Creates and executes a step with the provided input.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <typeparam name="TOut">The output type from the step</typeparam>
    /// <param name="previousStep">The input for the step</param>
    /// <param name="outVar">The output from the step</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(
        Either<Exception, TIn> previousStep,
        out Either<Exception, TOut> outVar
    )
        where TStep : IStep<TIn, TOut>, new() =>
        Chain<TStep, TIn, TOut>(new TStep(), previousStep, out outVar);

    /// <summary>
    /// Creates and executes a step with input extracted from Memory.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <typeparam name="TOut">The output type from the step</typeparam>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>()
        where TStep : IStep<TIn, TOut>, new() => Chain<TStep, TIn, TOut>(new TStep());

    #endregion

    #region Chain<TStep>

    /// <summary>
    /// Executes a step that is resolved from Memory by its interface type.
    /// </summary>
    /// <typeparam name="TStep">The interface type of the step to execute</typeparam>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Verifies that TStep is an interface
    /// 2. Extracts the step instance from Memory
    /// 3. Executes the step
    /// </remarks>
    // ReSharper disable once InconsistentNaming
    public Workflow<TInput, TReturn> IChain<TStep>()
        where TStep : class
    {
        var stepType = typeof(TStep);

        // Verify that TStep is an interface
        if (!stepType.IsInterface)
        {
            Exception ??= new WorkflowException(
                $"Step ({stepType}) must be an interface to call IChain."
            );

            return this;
        }

        // Extract the step instance from Memory
        var stepService = this.ExtractTypeFromMemory<TStep, TInput, TReturn>();

        if (stepService is null)
            return this;

        return Chain<TStep>(stepService);
    }

    /// <summary>
    /// Creates and executes a step by its type.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Creates an instance of the step
    /// 2. Executes the step
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep>()
        where TStep : class
    {
        if (Exception is not null)
            return this;

        // Create an instance of the step
        var stepInstance = this.InitializeStep<TStep, TInput, TReturn>();

        if (stepInstance is null)
            return this;

        return Chain<TStep>(stepInstance);
    }

    /// <summary>
    /// Executes a step instance.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <param name="stepInstance">The step instance to execute</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Extracts the input and output types from the step
    /// 2. Finds the appropriate Chain method to call
    /// 3. Executes the step
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep>(TStep stepInstance)
        where TStep : class
    {
        // Extract the input and output types from the step
        var (tIn, tOut) = ReflectionHelpers.ExtractStepTypeArguments<TStep>();

        // Find the appropriate Chain method to call
        var chainMethod = ReflectionHelpers.FindGenericChainMethod<TStep, TInput, TReturn>(
            this,
            tIn,
            tOut,
            1
        );

        // Execute the step
        var result = chainMethod.Invoke(this, [stepInstance]);

        return (Workflow<TInput, TReturn>)result!;
    }

    #endregion

    #region Chain<TStep, TIn>

    /// <summary>
    /// Executes a step with the provided input and Unit output.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <param name="step">The step instance to execute</param>
    /// <param name="previousStep">The input for the step</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This is a convenience method for steps that return Unit.
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(
        TStep step,
        Either<Exception, TIn> previousStep
    )
        where TStep : IStep<TIn, Unit> => Chain<TStep, TIn, Unit>(step, previousStep, out var x);

    /// <summary>
    /// Executes a step with input extracted from Memory and Unit output.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <param name="step">The step instance to execute</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This is a convenience method for steps that return Unit.
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step)
        where TStep : IStep<TIn, Unit> => Chain<TStep, TIn, Unit>(step);

    /// <summary>
    /// Creates and executes a step with the provided input and Unit output.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <param name="previousStep">The input for the step</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This is a convenience method for steps that return Unit.
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(Either<Exception, TIn> previousStep)
        where TStep : IStep<TIn, Unit>, new() =>
        Chain<TStep, TIn, Unit>(new TStep(), previousStep, out var x);

    /// <summary>
    /// Creates and executes a step with input extracted from Memory and Unit output.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This is a convenience method for steps that return Unit.
    /// </remarks>
    public Workflow<TInput, TReturn> Chain<TStep, TIn>()
        where TStep : IStep<TIn, Unit>, new() => Chain<TStep, TIn, Unit>(new TStep());

    #endregion
}
