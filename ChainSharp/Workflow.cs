using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp;

public abstract class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    private WorkflowException? Exception { get; set; }

    public async Task<TReturn> Run(TInput input)
        => await RunInternal(input).Unwrap();

    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(Either<WorkflowException, TIn> previousStep,
        out Either<WorkflowException, TOut> outVar)
        where TStep : IStep<TIn, TOut>, new()
    {
        if (Exception is not null)
        {
            outVar = Exception;
            return this;
        }
        
        outVar = Task.Run(() => new TStep().RailwayStep(previousStep)).Result;

        if (outVar.IsLeft)
            Exception = outVar.Swap().ValueUnsafe();
        
        return this; 
    }
    
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step, Either<WorkflowException, TIn> previousStep, out Either<WorkflowException, TOut> outVar)
        where TStep : IStep<TIn, TOut>
    {
        if (Exception is not null)
        {
            outVar = Exception;
            return this;
        }
        
        outVar = Task.Run(() => step.RailwayStep(previousStep)).Result;

        if (outVar.IsLeft)
            Exception = outVar.Swap().ValueUnsafe();
        
        return this;
    }
    
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step, Either<WorkflowException, TIn> previousStep)
        where TStep : IStep<TIn, Unit>
    {
        if (Exception is not null)
            return this;
        
        var stepResult = Task.Run(() => step.RailwayStep(previousStep)).Result;
        
        if (stepResult.IsLeft)
            Exception = stepResult.Swap().ValueUnsafe();
        
        return this;
    }
    
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(Either<WorkflowException, TIn> previousStep)
        where TStep : IStep<TIn, Unit>, new()
    {
        if (Exception is not null)
            return this;
        
        var stepResult = Task.Run(() => new TStep().RailwayStep(previousStep)).Result;
        
        if (stepResult.IsLeft)
            Exception = stepResult.Swap().ValueUnsafe();
        
        return this;
    }

    public Either<WorkflowException, TReturn> Resolve(Either<WorkflowException, TReturn> returnType)
        => Exception ?? returnType;

    protected abstract Task<Either<WorkflowException, TReturn>> RunInternal(TInput input);
}