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

    // Chain<TStep, TIn, TOut>(TStep, TIn, TOut)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(
        TStep step,
        Either<Exception, TIn> previousStep,
        out Either<Exception, TOut> outVar
    )
        where TStep : IStep<TIn, TOut>
    {
        if (Exception is not null)
        {
            outVar = Exception;
            return this;
        }

        outVar = Task.Run(() => step.RailwayStep(previousStep)).Result;

        if (outVar.IsLeft)
            Exception ??= outVar.Swap().ValueUnsafe();
        else
        {
            var outValue = outVar.Unwrap()!;

            if (typeof(TOut).IsTuple())
                this.AddTupleToMemory(outValue);
            else
                Memory[typeof(TOut)] = outValue;
        }

        return this;
    }

    // Chain<TStep, TIn, TOut>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step)
        where TStep : IStep<TIn, TOut>
    {
        var input = this.ExtractTypeFromMemory<TIn, TInput, TReturn>();

        if (input is null)
            return this;

        return Chain<TStep, TIn, TOut>(step, input, out var x);
    }

    // Chain<TStep, TIn, TOut>(TIn, TOut)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(
        Either<Exception, TIn> previousStep,
        out Either<Exception, TOut> outVar
    )
        where TStep : IStep<TIn, TOut>, new() =>
        Chain<TStep, TIn, TOut>(new TStep(), previousStep, out outVar);

    // Chain<TStep, TIn, TOut>()
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>()
        where TStep : IStep<TIn, TOut>, new() => Chain<TStep, TIn, TOut>(new TStep());

    #endregion

    #region Chain<TStep>

    // Chain<TStep>()
    // ReSharper disable once InconsistentNaming
    public Workflow<TInput, TReturn> IChain<TStep>()
        where TStep : class
    {
        var stepType = typeof(TStep);

        if (!stepType.IsInterface)
            Exception ??= new WorkflowException(
                $"Step ({stepType}) must be an interface to call IChain."
            );

        var stepService = this.ExtractTypeFromMemory<TStep, TInput, TReturn>();

        if (stepService is null)
            return this;

        return Chain<TStep>(stepService);
    }

    // Chain<TStep>()
    public Workflow<TInput, TReturn> Chain<TStep>()
        where TStep : class
    {
        var stepInstance = this.InitializeStep<TStep, TInput, TReturn>();

        if (stepInstance is null)
            return this;

        return Chain<TStep>(stepInstance);
    }

    // Chain<TStep>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep>(TStep stepInstance)
        where TStep : class
    {
        var (tIn, tOut) = ReflectionHelpers.ExtractStepTypeArguments<TStep>();
        var chainMethod = ReflectionHelpers.FindGenericChainMethod<TStep, TInput, TReturn>(
            this,
            tIn,
            tOut,
            1
        );

        var result = chainMethod.Invoke(this, [stepInstance]);

        return (Workflow<TInput, TReturn>)result!;
    }

    #endregion

    #region Chain<TStep, TIn>

    // Chain<TStep, TIn>(TStep, In)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(
        TStep step,
        Either<Exception, TIn> previousStep
    )
        where TStep : IStep<TIn, Unit> => Chain<TStep, TIn, Unit>(step, previousStep, out var x);

    // Chain<TStep, TIn>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step)
        where TStep : IStep<TIn, Unit> => Chain<TStep, TIn, Unit>(step);

    // Chain<TStep, TIn>(TIn)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(Either<Exception, TIn> previousStep)
        where TStep : IStep<TIn, Unit>, new() =>
        Chain<TStep, TIn, Unit>(new TStep(), previousStep, out var x);

    // Chain<TStep, TIn>()
    public Workflow<TInput, TReturn> Chain<TStep, TIn>()
        where TStep : IStep<TIn, Unit>, new() => Chain<TStep, TIn, Unit>(new TStep());

    #endregion
}
