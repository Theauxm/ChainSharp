using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Step;
using ChainSharp.Utils;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    /// <summary>
    /// Executes a step with short-circuit behavior, meaning that Left (exception) results
    /// are ignored and don't stop the workflow.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <typeparam name="TIn">The input type for the step</typeparam>
    /// <typeparam name="TOut">The output type from the step</typeparam>
    /// <param name="step">The step instance to execute</param>
    /// <param name="previousStep">The input for the step</param>
    /// <param name="outVar">The output from the step</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// Unlike regular Chain methods, ShortCircuitChain only stores the result in Memory
    /// if the step succeeds (returns Right). If it fails (returns Left), the workflow
    /// continues without setting an exception.
    /// </remarks>
    internal Workflow<TInput, TReturn> ShortCircuitChain<TStep, TIn, TOut>(
        TStep step,
        TIn previousStep,
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

        // Execute the step directly without thread pool scheduling
        var task = step.RailwayStep(previousStep, this);
        outVar = task.IsCompletedSuccessfully ? task.Result : task.GetAwaiter().GetResult();

        // We skip the Left for Short Circuiting - only process Right results
        if (outVar.IsRight)
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
    /// Executes a step with short-circuit behavior, potentially ending the workflow early
    /// if the step returns a value of type TReturn.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Creates an instance of the step
    /// 2. Executes it with short-circuit behavior
    /// 3. If the step returns a value of type TReturn, sets it as the ShortCircuitValue
    /// 4. When Resolve() is called, the ShortCircuitValue will be returned, bypassing the rest of the workflow
    /// </remarks>
    public Workflow<TInput, TReturn> ShortCircuit<TStep>()
        where TStep : class
    {
        // Create an instance of the step
        var stepInstance = this.InitializeStep<TStep, TInput, TReturn>();

        if (stepInstance is null)
            return this;

        return ShortCircuit<TStep>(stepInstance);
    }

    /// <summary>
    /// Executes a step with short-circuit behavior, potentially ending the workflow early
    /// if the step returns a value of type TReturn.
    /// </summary>
    /// <typeparam name="TStep">The type of step to execute</typeparam>
    /// <param name="stepInstance">The step instance to execute</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method:
    /// 1. Extracts the input and output types from the step
    /// 2. Finds the appropriate ShortCircuitChain method to call
    /// 3. Extracts the input from Memory
    /// 4. Executes the step
    /// 5. If the step returns a value of type TReturn, sets it as the ShortCircuitValue
    /// 6. When Resolve() is called, the ShortCircuitValue will be returned, bypassing the rest of the workflow
    /// </remarks>
    public Workflow<TInput, TReturn> ShortCircuit<TStep>(TStep stepInstance)
        where TStep : class
    {
        // Extract the input and output types from the step
        var (tIn, tOut) = ReflectionHelpers.ExtractStepTypeArguments<TStep>();

        // Find the appropriate ShortCircuitChain method to call
        var chainMethod = ReflectionHelpers.FindGenericChainInternalMethod<TStep, TInput, TReturn>(
            this,
            tIn,
            tOut,
            3
        );

        // Extract the input from Memory
        var input = this.ExtractTypeFromMemory(tIn);

        if (input is null)
        {
            Exception ??= new WorkflowException($"Could not find ({tIn}) in Memory.");
            return this;
        }

        // Execute the step
        object[] parameters = [stepInstance, input, null];
        var result = chainMethod.Invoke(this, parameters);
        var outParam = parameters[2];

        // If the step returns a value of type TReturn, set it as the ShortCircuitValue
        var maybeRightValue = ReflectionHelpers.GetRightFromDynamicEither(outParam);
        maybeRightValue.Iter(rightValue =>
        {
            ShortCircuitValue = (TReturn?)rightValue;
            ShortCircuitValueSet = true;
        });

        return (Workflow<TInput, TReturn>)result!;
    }
}
