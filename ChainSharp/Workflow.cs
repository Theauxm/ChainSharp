using System.Reflection;
using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Utils;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using static ChainSharp.Utils.ReflectionHelpers;
using static LanguageExt.Prelude;

namespace ChainSharp;

public abstract class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    private WorkflowException? Exception { get; set; }
    
    private Dictionary<Type, object> Memory { get; set; }
    
    private TReturn? ShortCircuitValue { get; set; }

    public async Task<TReturn> Run(TInput input)
    {
        Memory = new Dictionary<Type, object>();
        
        return await RunInternal(input).Unwrap();
    }

    public Workflow<TInput, TReturn> Activate(TInput input, params object[] otherTypes)
    {
        Memory ??= new Dictionary<Type, object>();
        
        if (input is null)
            Exception ??= new WorkflowException($"Input ({typeof(TInput)}) is null.");
        else 
            Memory.TryAdd(typeof(TInput), input);

        foreach (var otherType in otherTypes)
            Memory.TryAdd(otherType.GetType(), otherType);

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
    
     /// Chain<TStep, TIn, TOut>(TStep, TIn, TOut)
        public Workflow<TInput, TReturn> InternalChain<TStep, TIn, TOut>(TStep step, TIn previousStep, out Either<WorkflowException, TOut> outVar)
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
        var input = ExtractTypeFromMemory<TIn>();

        if (input is null) 
            return this;
        
        return Chain<TStep, TIn, TOut>(step, input, out var x);
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
    
    /// Chain<TStep>()
    public Workflow<TInput, TReturn> Chain<TStep>() where TStep : new()
    {
        var (tIn, tOut) = ExtractStepTypeArguments<TStep>();
        var chainMethod = FindGenericChainMethod<TStep, TInput, TReturn>(this, tIn, tOut, 1);
        var stepInstance = Activator.CreateInstance(typeof(TStep));
        var result = chainMethod.Invoke(this, new object[] { stepInstance });
        return (Workflow<TInput, TReturn>)result;
    }

    /// Chain<TStep>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep>(TStep stepInstance) where TStep : new()
    {
        var (tIn, tOut) = ExtractStepTypeArguments<TStep>();
        var chainMethod = FindGenericChainMethod<TStep, TInput, TReturn>(this, tIn, tOut, 1);
        var result = chainMethod.Invoke(this, new object[] { stepInstance });
        return (Workflow<TInput, TReturn>)result;
    }
    
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

    /// ShortCircuit<TStep>()
    public Workflow<TInput, TReturn> ShortCircuit<TStep>() where TStep : new()
    {
        var (tIn, tOut) = ExtractStepTypeArguments<TStep>();
        var chainMethod = 
            FindGenericChainInternalMethod<TStep, TInput, TReturn>(this, tIn, tOut, 3);
        var stepInstance = Activator.CreateInstance(typeof(TStep));
        var input = ExtractTypeFromMemory(tIn);
        if (input is null) 
            return this;
            
        object[] parameters = [stepInstance, input, null];
        var result = chainMethod.Invoke(this, parameters);
        var outParam = parameters[2];

        var maybeRightValue = GetRightFromDynamicEither(outParam);
        maybeRightValue.Iter(rightValue => ShortCircuitValue = (TReturn?)rightValue);
        
        var workflow = (Workflow<TInput, TReturn>)result;
        return workflow;
    }
    
    /// ShortCircuit<TStep>(TStep)
    public Workflow<TInput, TReturn> ShortCircuit<TStep>(TStep stepInstance) where TStep : new()
    {
        var (tIn, tOut) = ExtractStepTypeArguments<TStep>();
        var chainMethod = 
            FindGenericChainInternalMethod<TStep, TInput, TReturn>(this, tIn, tOut, 3);
        var input = ExtractTypeFromMemory(tIn);
        if (input is null) 
            return this;
            
        object[] parameters = [stepInstance, input, null];
        var result = chainMethod.Invoke(this, parameters);
        var outParam = parameters[2];

        var maybeRightValue = GetRightFromDynamicEither(outParam);
        maybeRightValue.Iter(rightValue => ShortCircuitValue = (TReturn?)rightValue);
        
        var workflow = (Workflow<TInput, TReturn>)result;
        return workflow;
    }

    public Either<WorkflowException, TReturn> Resolve(Either<WorkflowException, TReturn> returnType)
        => Exception ?? returnType;

    public Either<WorkflowException, TReturn> Resolve()
    {
        if (Exception is not null)
            return Exception;

        if (ShortCircuitValue is not null)
            return ShortCircuitValue;
        
        var result = Memory.GetValueOrDefault(
                typeof(TReturn));
        
        if (result is null)
            return new WorkflowException($"Could not find type: ({typeof(TReturn)}).");

        return (TReturn)result;
    }

    private T? ExtractTypeFromMemory<T>()
    {
        T? input = default;

        var inputType = typeof(T);
        if (inputType.IsTuple())
        {
            try
            {
                input = ExtractTuple(inputType);
            }
            catch (Exception e)
            {
                Exception ??= new WorkflowException(e.Message);
            }
        }

        input ??= (T?)Memory.GetValueOrDefault(inputType);
        
        if (input is null)
            Exception ??= new WorkflowException($"Could not find type: ({inputType}).");

        return input;
    }
    
    private dynamic ExtractTypeFromMemory(Type tIn)
    {
        dynamic? input = null;

        var inputType = tIn;
        if (inputType.IsTuple())
        {
            try
            {
                input = ExtractTuple(inputType);
            }
            catch (Exception e)
            {
                Exception ??= new WorkflowException(e.Message);
            }
        }

        input ??= Memory.GetValueOrDefault(inputType);
        
        if (input is null)
            Exception ??= new WorkflowException($"Could not find type: ({inputType}).");

        return input!;
    }
    
    private dynamic ExtractTuple(Type inputType)
    {
        var dynamicList = TypeHelpers.ExtractTypes(Memory, inputType);

        return dynamicList.Count switch
        {
            0 => throw new WorkflowException($"Cannot have Tuple of length 0."),
            2 => TypeHelpers.ConvertTwoTuple(dynamicList),
            3 => TypeHelpers.ConvertThreeTuple(dynamicList),
            4 => TypeHelpers.ConvertFourTuple(dynamicList),
            5 => TypeHelpers.ConvertFiveTuple(dynamicList),
            6 => TypeHelpers.ConvertSixTuple(dynamicList),
            7 => TypeHelpers.ConvertSevenTuple(dynamicList),
            _ => throw new WorkflowException($"Could not create Tuple for type ({inputType})")
        };
    }

    protected abstract Task<Either<WorkflowException, TReturn>> RunInternal(TInput input);
}