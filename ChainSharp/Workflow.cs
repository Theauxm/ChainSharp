using System.Runtime.CompilerServices;
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

    public Workflow<TInput, TReturn> AddServices<T1>(T1 service)
    {
        Type[] typeArray = [typeof(T1)];

        return AddServices([service], typeArray);
    }

    public Workflow<TInput, TReturn> AddServices<T1, T2>(T1 service1, T2 service2)
    {
        Type[] typeArray = [typeof(T1), typeof(T2)];
        object[] services = [service1, service2];

        return AddServices(services, typeArray);
    }

    public Workflow<TInput, TReturn> AddServices<T1, T2, T3>(T1 service1, T2 service2, T3 service3)
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3)];
        object[] services = [service1, service2, service3];

        return AddServices(services, typeArray);
    }

    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4>(
        T1 service1, T2 service2, T3 service3, T4 service4
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4)];
        object[] services = [service1, service2, service3, service4];

        return AddServices(services, typeArray);
    }

    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4, T5>(
        T1 service1, T2 service2, T3 service3, T4 service4, T5 service5
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)];
        object[] services = [service1, service2, service3, service4, service5];

        return AddServices(services, typeArray);
    }

    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4, T5, T6>(
        T1 service1, T2 service2, T3 service3, T4 service4, T5 service5, T6 service6
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)];
        object[] services = [service1, service2, service3, service4, service5, service6];

        return AddServices(services, typeArray);
    }

    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4, T5, T6, T7>(
        T1 service1, T2 service2, T3 service3, T4 service4, T5 service5, T6 service6, T7 service7
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)];
        object[] services = [service1, service2, service3, service4, service5, service6, service7];

        return AddServices(services, typeArray);
    } 
    
    private Workflow<TInput, TReturn> AddServices(object[] services, Type[] typeArray)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() { { typeof(Unit), unit } };
        
        foreach (var service in services)
        {
            var serviceType = service.GetType();

            if (serviceType.IsMoqProxy())
            {
                var mockedType = service.GetMockedTypeFromObject();
                Memory.TryAdd(mockedType, service);
                continue;
            }

            if (!serviceType.IsClass)
                throw new WorkflowException($"Params ({serviceType}) to AddServices must be Classes.");

            var interfaces = serviceType.GetInterfaces();
            var foundInterface = interfaces.FirstOrDefault(x => typeArray.Contains(x));

            if (foundInterface is null)
                throw new WorkflowException($"Class ({serviceType}) does not have any interfaces.");

            Memory.TryAdd(foundInterface, service);
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
        {
            Exception ??= new WorkflowException($"Input ({inputType}) is null.");
            return this;
        }

        if (inputType.IsTuple())
            AddTupleToMemory(input);
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
        else
        {
            var outValue = outVar.Unwrap()!;

            if (typeof(TOut).IsTuple())
                AddTupleToMemory(outValue);
            else
                Memory.TryAdd(typeof(TOut), outValue);
        }
        
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
        {
            var outValue = outVar.Unwrap()!;

            if (typeof(TOut).IsTuple())
                AddTupleToMemory(outValue);
            else
                Memory.TryAdd(typeof(TOut), outValue);
        }
            
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
                Exception ??= e;
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
                Exception ??= e;
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
    
    private Unit AddTupleToMemory<TIn>(TIn input)
    {
        if (!typeof(TIn).IsTuple())
            throw new WorkflowException($"({typeof(TIn)}) is not a Tuple but was attempted to be extracted as one.");

        if (input is null)
            throw new WorkflowException($"Input of type ({typeof(TIn)} cannot be null.");

        var inputTuple = (ITuple)input;
        
        var tupleList = Enumerable
            .Range(0, inputTuple.Length)
            .Select(i => inputTuple[i]!)
            .ToList();

        foreach (var tupleValue in tupleList)
            Memory.TryAdd(tupleValue.GetType(), tupleValue);
        
        return Unit.Default;
    } 

    #endregion
}