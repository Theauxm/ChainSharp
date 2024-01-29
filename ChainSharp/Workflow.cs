using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp;

public abstract class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    private WorkflowException? Exception { get; set; }
    
    private Dictionary<Type, Either<WorkflowException, Object>> Memory { get; set; }

    public async Task<TReturn> Run(TInput input)
    {
        Memory = new Dictionary<Type, Either<WorkflowException, Object>>();
        
        return await RunInternal(input).Unwrap();
    }

    /// <summary>
    /// Chain<TStep, TIn, TOut>(TIn, TOut)
    /// </summary>
    /// <param name="previousStep"></param>
    /// <param name="outVar"></param>
    /// <typeparam name="TStep"></typeparam>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
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
    
    /// <summary>
    /// Chain<TStep, TIn, TOut>(TStep)
    /// </summary>
    /// <param name="step"></param>
    /// <typeparam name="TStep"></typeparam>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step)
        where TStep : IStep<TIn, TOut>
    {
        if (Exception is not null)
        {
            return this;
        }

        Either<WorkflowException, TIn> input;
        try
        {
            input = (Either<WorkflowException, TIn>)Memory[typeof(Either<WorkflowException, TIn>)];
        }
        catch (Exception e)
        {
            Exception = new WorkflowException($"Could not find type: ({typeof(Either<WorkflowException, TIn>)}). {e}");
            return this;
        }

        var result = Task.Run(() => step.RailwayStep(input)).Result;
        
        Memory.Add(typeof(TOut), result);

        if (result.IsLeft)
            Exception = result.Swap().ValueUnsafe();
        
        return this;
    }
    
    /// <summary>
    /// Chain<TStep, TIn, TOut>(TStep)
    /// </summary>
    /// <param name="step"></param>
    /// <typeparam name="TStep"></typeparam>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>()
        where TStep : IStep<TIn, TOut>, new()
    {
        if (Exception is not null)
        {
            return this;
        }

        Either<WorkflowException, TIn> input;
        try
        {
            input = (Either<WorkflowException, TIn>)Memory[typeof(Either<WorkflowException, TIn>)];
        }
        catch (Exception e)
        {
            Exception = new WorkflowException($"Could not find type: ({typeof(Either<WorkflowException, TIn>)}). {e}");
            return this;
        }

        var result = Task.Run(() => new TStep().RailwayStep(input)).Result;
        
        Memory.Add(typeof(TOut), result);

        if (result.IsLeft)
            Exception = result.Swap().ValueUnsafe();
        
        return this;
    }
    
    /// <summary>
    /// Chain<TStep, TIn>(TStep)
    /// </summary>
    /// <param name="step"></param>
    /// <typeparam name="TStep"></typeparam>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step)
        where TStep : IStep<TIn, Unit>
    {
        if (Exception is not null)
        {
            return this;
        }

        Either<WorkflowException, TIn> input;
        try
        {
            input = (Either<WorkflowException, TIn>)Memory[typeof(Either<WorkflowException, TIn>)];
        }
        catch (Exception e)
        {
            Exception = new WorkflowException($"Could not find type: ({typeof(Either<WorkflowException, TIn>)}). {e}");
            return this;
        }

        var result = Task.Run(() => step.RailwayStep(input)).Result;

        if (result.IsLeft)
            Exception = result.Swap().ValueUnsafe();
        
        return this;
    }
    
    /// <summary>
    /// Chain<TStep, TIn>()
    /// </summary>
    /// <param name="step"></param>
    /// <typeparam name="TStep"></typeparam>
    /// <typeparam name="TIn"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
    public Workflow<TInput, TReturn> Chain<TStep, TIn>()
        where TStep : IStep<TIn, Unit>, new()
    {
        if (Exception is not null)
        {
            return this;
        }

        Either<WorkflowException, TIn> input;
        try
        {
            input = (Either<WorkflowException, TIn>)Memory[typeof(Either<WorkflowException, TIn>)];
        }
        catch (Exception e)
        {
            Exception = new WorkflowException($"Could not find type: ({typeof(Either<WorkflowException, TIn>)}). {e}");
            return this;
        }

        var result = Task.Run(() => new TStep().RailwayStep(input)).Result;

        if (result.IsLeft)
            Exception = result.Swap().ValueUnsafe();
        
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