using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Utils;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.DependencyInjection;
using static ChainSharp.Utils.ReflectionHelpers;
using static LanguageExt.Prelude;

namespace ChainSharp;

public abstract class Workflow<TInput, TReturn> : IWorkflow<TInput, TReturn>
{
    private Exception? Exception { get; set; }
    
    private Dictionary<Type, object> Memory { get; set; }
    
    private TReturn? ShortCircuitValue { get; set; }

    public async Task<TReturn> Run(TInput input)
        => await RunEither(input).Unwrap();
    
    public async Task<Either<Exception, TReturn>> RunEither(TInput input)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory = new Dictionary<Type, object>() {{ typeof(Unit), unit }};
        
        return await RunInternal(input);
    }
    
    protected abstract Task<Either<Exception, TReturn>> RunInternal(TInput input);

    #region AddServices

    public Workflow<TInput, TReturn> AddServices(params object[] services)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() {{ typeof(Unit), unit }};

        foreach (var service in services)
        {
            var serviceType = service.GetType();

            if (!serviceType.IsClass)
                throw new WorkflowException($"Params ({serviceType}) to AddServices must be Classes."); 
            
            var serviceInterface = serviceType
                .GetInterfaces()
                .FirstOrDefault(x => !x.IsGenericType || x.GetGenericTypeDefinition() != typeof(IStep<,>));

            if (serviceInterface is null)
                throw new WorkflowException($"Class ({serviceType}) does not have any interfaces.");

            Memory.TryAdd(serviceInterface, service);
        }

        return this;
    } 

    #endregion

    #region Activate

    public Workflow<TInput, TReturn> Activate(TInput input, params object[] otherTypes)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() {{ typeof(Unit), unit }};

        var inputType = typeof(TInput);
        
        if (input is null)
            Exception ??= new WorkflowException($"Input ({inputType}) is null.");
        else 
            Memory.TryAdd(inputType, input);

        foreach (var otherType in otherTypes)
            Memory.TryAdd(otherType.GetType(), otherType);

        return this;
    } 

    #endregion

    #region Chain<TStep, TIn, TOut>

    // Chain<TStep, TIn, TOut>(TStep, TIn, TOut)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step, Either<Exception, TIn> previousStep, out Either<Exception, TOut> outVar)
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

        if (outVar.IsRight)
            Memory.TryAdd(typeof(TOut), outVar.Unwrap()!);
        
        return this;
    }

    // Chain<TStep, TIn, TOut>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(TStep step)
        where TStep : IStep<TIn, TOut>
    {
        var input = ExtractTypeFromMemory<TIn>();

        if (input is null) 
            return this;
        
        return Chain<TStep, TIn, TOut>(step, input, out var x);
    }
    
    // Chain<TStep, TIn, TOut>(TIn, TOut)
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>(Either<Exception, TIn> previousStep,
        out Either<Exception, TOut> outVar)
        where TStep : IStep<TIn, TOut>, new()
        => Chain<TStep, TIn, TOut>(new TStep(), previousStep, out outVar);
    
    // Chain<TStep, TIn, TOut>()
    public Workflow<TInput, TReturn> Chain<TStep, TIn, TOut>()
        where TStep : IStep<TIn, TOut>, new() => Chain<TStep, TIn, TOut>(new TStep()); 

    #endregion
    
    #region Chain<TStep>
    
    // Chain<TStep>()
    // ReSharper disable once InconsistentNaming
    public Workflow<TInput, TReturn> IChain<TStep>() where TStep : class
    {
        var stepType = typeof(TStep);

        if (!stepType.IsInterface)
            Exception ??= new WorkflowException($"Step ({stepType}) must be an interface to call IChain.");
        
        var stepService = ExtractTypeFromMemory<TStep>();

        if (stepService is null)
            return this;

        return Chain<TStep>(stepService);
    }
    
    // Chain<TStep>()
    public Workflow<TInput, TReturn> Chain<TStep>() where TStep : class
    {
        var stepInstance = InitializeStep<TStep>();

        if (stepInstance is null)
            return this;

        return Chain<TStep>(stepInstance);
    }

    // Chain<TStep>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep>(TStep stepInstance) where TStep : class
    {
        var (tIn, tOut) = ExtractStepTypeArguments<TStep>();
        var chainMethod = FindGenericChainMethod<TStep, TInput, TReturn>(this, tIn, tOut, 1);
        
        var result = chainMethod.Invoke(this, [stepInstance]);
        
        return (Workflow<TInput, TReturn>)result!;
    }
    
    #endregion

    #region Chain<TStep, TIn>
       
    // Chain<TStep, TIn>(TStep, In)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step, Either<Exception, TIn> previousStep)
        where TStep : IStep<TIn, Unit>
        => Chain<TStep, TIn, Unit>(step, previousStep, out var x);
    
    // Chain<TStep, TIn>(TStep)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(TStep step)
        where TStep : IStep<TIn, Unit>
        => Chain<TStep, TIn, Unit>(step);


    // Chain<TStep, TIn>(TIn)
    public Workflow<TInput, TReturn> Chain<TStep, TIn>(Either<Exception, TIn> previousStep)
        where TStep : IStep<TIn, Unit>, new()
        => Chain<TStep, TIn, Unit>(new TStep(), previousStep, out var x);
    
    // Chain<TStep, TIn>()
    public Workflow<TInput, TReturn> Chain<TStep, TIn>()
        where TStep : IStep<TIn, Unit>, new()
        => Chain<TStep, TIn, Unit>(new TStep()); 

    #endregion

    #region ShortCircuit
    
    // ShortCircuitChain<TStep, TIn, TOut>(TStep, TIn, TOut)
    public Workflow<TInput, TReturn> ShortCircuitChain<TStep, TIn, TOut>(TStep step, TIn previousStep, out Either<Exception, TOut> outVar)
        where TStep : IStep<TIn, TOut>
    {
        if (Exception is not null)
        {
            outVar = Exception;
            return this;
        }
            
        outVar = Task.Run(() => step.RailwayStep(previousStep)).Result;
    
        // We skip the Left for Short Circuiting
        if (outVar.IsRight)
            Memory.TryAdd(typeof(TOut), outVar.Unwrap()!);
            
        return this;
    } 



    // ShortCircuit<TStep>()
    public Workflow<TInput, TReturn> ShortCircuit<TStep>() where TStep : class
    {
        var stepInstance = InitializeStep<TStep>();

        if (stepInstance is null)
            return this;

        return ShortCircuit<TStep>(stepInstance);
    }
    
    // ShortCircuit<TStep>(TStep)
    public Workflow<TInput, TReturn> ShortCircuit<TStep>(TStep stepInstance) where TStep : class
    {
        var (tIn, tOut) = ExtractStepTypeArguments<TStep>();
        var chainMethod = 
            FindGenericChainInternalMethod<TStep, TInput, TReturn>(this, tIn, tOut, 3);
        var input = ExtractTypeFromMemory(tIn);

        if (input == null)
            return this;
            
        object[] parameters = [stepInstance, input, null];
        var result = chainMethod.Invoke(this, parameters);
        var outParam = parameters[2];

        var maybeRightValue = GetRightFromDynamicEither(outParam);
        maybeRightValue.Iter(rightValue => ShortCircuitValue = (TReturn?)rightValue);
        
        return (Workflow<TInput, TReturn>)result!;
    } 

    #endregion

    #region Resolve

    public Either<Exception, TReturn> Resolve(Either<Exception, TReturn> returnType)
        => Exception ?? returnType;

    public Either<Exception, TReturn> Resolve()
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

    #endregion

    #region Helpers

    private TStep? InitializeStep<TStep>() where TStep : class
    {
        var stepType = typeof(TStep);

        if (!stepType.IsClass)
        {
            Exception ??= new WorkflowException($"Step ({stepType}) must be a class.");
            return null;
        }
        
        var constructors = stepType.GetConstructors();

        if (constructors.Length != 1)
        {
            Exception ??= new WorkflowException($"Step classes can only have a single constructor ({stepType}).");
            return null;
        }

        var constructorArguments = constructors
            .First()
            .GetParameters()
            .Select(x => x.ParameterType)
            .ToArray();

        var constructor = stepType.GetConstructor(constructorArguments);

        if (constructor is null)
        {
            Exception ??= new WorkflowException($"Could not find constructor for ({stepType})");
            return null;
        }
        
        var constructorParameters = ExtractTypesFromMemory(constructorArguments);
    
        var initializedStep = (TStep?)constructor.Invoke(constructorParameters);

        if (initializedStep is null)
        {
            Exception ??= new WorkflowException($"Could not invoke constructor for ({stepType}).");
            return null;
        }

        return initializedStep;
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

    private dynamic[] ExtractTypesFromMemory(IEnumerable<Type> types)
        => types.Select(type => ExtractTypeFromMemory(type)).ToArray();
    
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

    #endregion
}