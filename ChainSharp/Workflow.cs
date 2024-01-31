using System.Reflection;
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
        var stepType = typeof(TStep);
        var interfaceType = stepType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStep<,>));
        if (interfaceType == null)
        {
            throw new InvalidOperationException($"{nameof(TStep)} does not implement IStep<TIn, TOut>.");
        }

        var types = interfaceType.GetGenericArguments();
        var tIn = types[0];
        var tOut = types[1];

        // Find a Generic Chain with 3 Type arguments and 1 Parameter
        var methods = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m is { Name: "Chain", IsGenericMethodDefinition: true })
            .Where(m => m.GetGenericArguments().Length == 3)
            .Where(m => m.GetParameters().Length == 1)
            .ToList();

        var method = methods.FirstOrDefault(); // For example, if you know the count

        if (method == null)
        {
            throw new InvalidOperationException("Suitable 'Chain' method not found.");
        }
        
        var genericMethod = method.MakeGenericMethod(typeof(TStep), tIn, tOut);

        var stepInstance = Activator.CreateInstance(typeof(TStep));
        var result = genericMethod.Invoke(this, new object[] { stepInstance });
        
        return (Workflow<TInput, TReturn>)result;
    }

    /// Chain<TStep>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep>(TStep stepInstance) where TStep : new()
    {
        var stepType = typeof(TStep);
        var interfaceType = stepType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStep<,>));
        if (interfaceType == null)
        {
            throw new InvalidOperationException($"{nameof(TStep)} does not implement IStep<TIn, TOut>.");
        }

        var types = interfaceType.GetGenericArguments();
        var tIn = types[0];
        var tOut = types[1];

        // Find a Generic Chain with 3 Type arguments and 1 Parameter
        var methods = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m is { Name: "Chain", IsGenericMethodDefinition: true })
            .Where(m => m.GetGenericArguments().Length == 3)
            .Where(m => m.GetParameters().Length == 1)
            .ToList();

        var method = methods.FirstOrDefault(); // For example, if you know the count

        if (method == null)
        {
            throw new InvalidOperationException("Suitable 'Chain' method not found.");
        }
        
        var genericMethod = method.MakeGenericMethod(typeof(TStep), tIn, tOut);

        var result = genericMethod.Invoke(this, new object[] { stepInstance });
        
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