using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Step;
using ChainSharp.Utils;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    // ShortCircuitChain<TStep, TIn, TOut>(TStep, TIn, TOut)
    public Workflow<TInput, TReturn> ShortCircuitChain<TStep, TIn, TOut>(
        TStep step,
        TIn previousStep,
        out Either<Exception, TOut> outVar
    )
        where TStep : IStep<TIn, TOut>
    {
        if (Exception is not null)
        {
            outVar = Exception;
            return this;
        }

        outVar = Task.Run(() => step.RailwayStep(previousValue)).Result;

        // We skip the Left for Short Circuiting
        if (outVar.IsRight)
        {
            var outValue = outVar.Unwrap()!;

            if (typeof(TOut).IsTuple())
                this.AddTupleToMemory(outValue);
            else
                Memory[typeof(TOut)] = outValue;
        }

        return this;
    }

    // ShortCircuit<TStep>()
    public Workflow<TInput, TReturn> ShortCircuit<TStep>()
        where TStep : class
    {
        var stepInstance = this.InitializeStep<TStep, TInput, TReturn>();

        if (stepInstance is null)
            return this;

        return ShortCircuit<TStep>(stepInstance);
    }

    // ShortCircuit<TStep>(TStep)
    public Workflow<TInput, TReturn> ShortCircuit<TStep>(TStep stepInstance)
        where TStep : class
    {
        var (tIn, tOut) = ReflectionHelpers.ExtractStepTypeArguments<TStep>();
        var chainMethod = ReflectionHelpers.FindGenericChainInternalMethod<TStep, TInput, TReturn>(
            this,
            tIn,
            tOut,
            3
        );
        var input = this.ExtractTypeFromMemory(tIn);

        if (input == null)
        {
            Exception ??= new WorkflowException($"Could not find ({tIn}) in Memory.");
            return this;
        }

        object[] parameters = [stepInstance, input, null];
        var result = chainMethod.Invoke(this, parameters);
        var outParam = parameters[2];

        var maybeRightValue = ReflectionHelpers.GetRightFromDynamicEither(outParam);
        maybeRightValue.Iter(rightValue =>
        {
            ShortCircuitValue = (TReturn?)rightValue;
            ShortCircuitValueSet = true;
        });

        return (Workflow<TInput, TReturn>)result!;
    }
}
