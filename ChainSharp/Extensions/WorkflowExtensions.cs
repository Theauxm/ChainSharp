using System.Runtime.CompilerServices;
using ChainSharp.Exceptions;
using ChainSharp.Utils;
using ChainSharp.Workflow;
using LanguageExt;

namespace ChainSharp.Extensions;

public static class WorkflowExtensions
{
    internal static TStep? InitializeStep<TStep, TInput, TReturn>(this Workflow<TInput, TReturn> workflow) where TStep : class
    {
        var stepType = typeof(TStep);

        if (!stepType.IsClass)
        {
            workflow.Exception ??= new WorkflowException($"Step ({stepType}) must be a class.");
            return null;
        }
        
        var constructors = stepType.GetConstructors();

        if (constructors.Length != 1)
        {
            workflow.Exception ??= new WorkflowException($"Step classes can only have a single constructor ({stepType}).");
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
            workflow.Exception ??= new WorkflowException($"Could not find constructor for ({stepType})");
            return null;
        }
        
        var constructorParameters = ExtractTypesFromMemory(workflow, constructorArguments);
    
        var initializedStep = (TStep?)constructor.Invoke(constructorParameters);

        if (initializedStep is null)
        {
            workflow.Exception ??= new WorkflowException($"Could not invoke constructor for ({stepType}).");
            return null;
        }

        return initializedStep;
    }

    internal static T? ExtractTypeFromMemory<T, TInput, TReturn>(this Workflow<TInput, TReturn> workflow)
    {
        T? input = default;

        var inputType = typeof(T);
        if (inputType.IsTuple())
        {
            try
            {
                input = ExtractTuple(workflow, inputType);
            }
            catch (Exception e)
            {
                workflow.Exception ??= e;
            }
        }

        input ??= (T?)workflow.Memory.GetValueOrDefault(inputType);
        
        if (input is null)
            workflow.Exception ??= new WorkflowException($"Could not find type: ({inputType}).");

        return input;
    }

    internal static dynamic[] ExtractTypesFromMemory<TInput, TReturn>(this Workflow<TInput, TReturn> workflow, IEnumerable<Type> types)
        => types.Select(type => ExtractTypeFromMemory(workflow, type)).ToArray();
    
    internal static dynamic ExtractTypeFromMemory<TInput, TReturn>(this Workflow<TInput, TReturn> workflow, Type tIn)
    {
        dynamic? input = null;

        var inputType = tIn;
        if (inputType.IsTuple())
        {
            try
            {
                input = ExtractTuple(workflow, inputType);
            }
            catch (Exception e)
            {
                workflow.Exception ??= e;
            }
        }

        input ??= workflow.Memory.GetValueOrDefault(inputType);
        
        if (input is null)
            workflow.Exception ??= new WorkflowException($"Could not find type: ({inputType}).");

        return input!;
    }
    
    internal static dynamic ExtractTuple<TInput, TReturn>(this Workflow<TInput, TReturn> workflow, Type inputType)
    {
        var typeTuples = TypeHelpers.ExtractTypeTuples(workflow.Memory, inputType);

        return typeTuples.Count switch
        {
            0 => throw new WorkflowException($"Cannot have Tuple of length 0."),
            2 => TypeHelpers.ConvertTwoTuple(typeTuples),
            3 => TypeHelpers.ConvertThreeTuple(typeTuples),
            4 => TypeHelpers.ConvertFourTuple(typeTuples),
            5 => TypeHelpers.ConvertFiveTuple(typeTuples),
            6 => TypeHelpers.ConvertSixTuple(typeTuples),
            7 => TypeHelpers.ConvertSevenTuple(typeTuples),
            _ => throw new WorkflowException($"Could not create Tuple for type ({inputType})")
        };
    }

    internal static Unit AddTupleToMemory<TIn, TInput, TReturn>(this Workflow<TInput, TReturn> workflow, TIn input)
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
            workflow.Memory[tupleValue.GetType()] = tupleValue;
        
        return Unit.Default;
    } 
}