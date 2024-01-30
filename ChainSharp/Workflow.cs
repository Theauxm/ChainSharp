using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Utils;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp;

public abstract class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    private WorkflowException? Exception { get; set; }
    
    private Dictionary<Type, object> Memory { get; set; }

    public async Task<TReturn> Run(TInput input)
    {
        Memory = new Dictionary<Type, object>();
        
        return await RunInternal(input).Unwrap();
    }

    public Workflow<TInput, TReturn> Activate(TInput input)
    {
        if (input is null)
            Exception ??= new WorkflowException($"Input ({typeof(TInput)}) is null.");
        else 
            Memory.Add(typeof(TInput), input);

        return this;
    }
    
    /// Current Implementations:
    /// Chain<TStep, TIn, TOut>(TStep, TIn, TOut)
    /// Chain<TStep, TIn, TOut>(TIn, TOut)
    /// Chain<TStep, TIn, TOut>(TStep)
    /// Chain<TStep, TIn, TOut>()
    /// Chain<TStep, TIn>(TStep, TIn)
    /// Chain<TStep, TIn>(TIn)
    /// Chain<TStep, TIn>(TStep)
    /// Chain<TStep, TIn>()
    
    /// Chain<TStep, TIn, TOut>(TStep, TIn, TOut)
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
            Exception ??= outVar.Swap().ValueUnsafe();

        Memory.TryAdd(typeof(TOut), outVar.Unwrap());
        
        return this;
    }

    /// Chain<TStep, TIn, TOut>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step)
        where TStep : IStep<TIn, TOut>
    {
        TIn? input = default;
        
        var inputType = typeof(TIn);
        if (inputType.IsTuple())
        {
            try
            {
                input = TypeHelpers.ExtractTuple(Memory, inputType);
            }
            catch (Exception e)
            {
                Exception ??= new WorkflowException(e.Message);
                return this;
            }
        }

        input ??= (TIn?)Memory.GetValueOrDefault(inputType);

        if (input is not null) 
            return Chain<TStep, TIn, TOut>(step, input, out var x);
        
        Exception ??= new WorkflowException($"Could not find type: ({inputType}).");
        
        return this;
    }
    
    /// Chain<TStep, TIn, TOut>(TIn, TOut)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(Either<WorkflowException, TIn> previousStep,
        out Either<WorkflowException, TOut> outVar)
        where TStep : IStep<TIn, TOut>, new()
        => Chain<TStep, TIn, TOut>(new TStep(), previousStep, out outVar);
    
    /// Chain<TStep, TIn, TOut>()
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>()
        where TStep : IStep<TIn, TOut>, new()
        => Chain<TStep, TIn, TOut>(new TStep());

    
    /// Chain<TStep, TIn>(TStep, In)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step, Either<WorkflowException, TIn> previousStep)
        where TStep : IStep<TIn, Unit>
        => Chain<TStep, TIn, Unit>(step, previousStep, out var x);
    
    /// Chain<TStep, TIn>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step)
        where TStep : IStep<TIn, Unit>
        => Chain<TStep, TIn, Unit>(step);


    /// Chain<TStep, TIn>(TIn)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(Either<WorkflowException, TIn> previousStep)
        where TStep : IStep<TIn, Unit>, new()
        => Chain<TStep, TIn, Unit>(new TStep(), previousStep, out var x);
    
    /// Chain<TStep, TIn>()
    public Workflow<TInput, TReturn> Chain<TStep, TIn>()
        where TStep : IStep<TIn, Unit>, new()
        => Chain<TStep, TIn, Unit>(new TStep());

    
    public Either<WorkflowException, TReturn> Resolve(Either<WorkflowException, TReturn> returnType)
        => Exception ?? returnType;

    public Either<WorkflowException, TReturn> Resolve()
    {
        if (Exception is not null)
            return Exception;
        
        var result = Memory.GetValueOrDefault(
                typeof(TReturn));
        
        if (result is null)
            return new WorkflowException($"Could not find type: ({typeof(TReturn)}).");

        return (TReturn)result;
    }

    protected abstract Task<Either<WorkflowException, TReturn>> RunInternal(TInput input);
}