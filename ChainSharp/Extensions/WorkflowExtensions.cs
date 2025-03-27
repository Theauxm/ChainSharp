using System.Runtime.CompilerServices;
using ChainSharp.Exceptions;
using ChainSharp.Utils;
using ChainSharp.Workflow;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Extensions;

public static class WorkflowExtensions
{
    internal static TStep? InitializeStep<TStep, TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow
    )
        where TStep : class
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
            workflow.Exception ??= new WorkflowException(
                $"Step classes can only have a single constructor ({stepType})."
            );
            return null;
        }

        var constructorArguments = constructors
            .First()
            .GetParameters()
            .Select(x => x.ParameterType)
            .ToArray();

        var constructor = stepType.GetConstructor(constructorArguments);

        if (constructor == null)
        {
            workflow.Exception ??= new WorkflowException(
                $"Could not find constructor for ({stepType})"
            );
            return null;
        }

        var constructorParameters = ExtractTypesFromMemory(workflow, constructorArguments);

        var initializedStep = (TStep?)constructor.Invoke(constructorParameters);

        if (initializedStep is null)
        {
            workflow.Exception ??= new WorkflowException(
                $"Could not invoke constructor for ({stepType})."
            );
            return null;
        }

        return initializedStep;
    }

    internal static dynamic?[] ExtractTypesFromMemory<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        IEnumerable<Type> types
    ) => types.Select(type => ExtractTypeFromMemory(workflow, type)).ToArray();

    internal static T? ExtractTypeFromMemory<T, TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow
    )
    {
        var type = workflow.ExtractTypeFromMemory(typeof(T));

        return type is null ? default : (T)type;
    }

    internal static dynamic? ExtractLoggerFromLoggerFactory<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type tIn
    )
    {
        if (tIn.IsGenericType == false || tIn.GetGenericTypeDefinition() != typeof(ILogger<>))
            return null;

        if (
            workflow.Memory.GetValueOrDefault(typeof(ILoggerFactory))
            is not ILoggerFactory loggerFactory
        )
            throw new WorkflowException(
                $"Could not find ILoggerFactory for input type: ({tIn}). Have you injected an ILoggerFactory into the Workflow's services?"
            );

        var generics = tIn.GetGenericArguments();

        if (generics.Length != 1)
            throw new WorkflowException(
                $"Incorrect number of generic arguments for input type ({tIn}). Found ({generics.Length}) generics with types ({string.Join(", ", generics.Select(x => x.Name))})."
            );

        return loggerFactory.CreateGenericLogger(generics.First());
    }

    internal static dynamic? ExtractTypeFromServiceProvider<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type tIn
    )
    {
        var service = workflow.Memory.GetValueOrDefault(typeof(IServiceProvider))
            is IServiceProvider serviceProvider
            ? serviceProvider.GetService(tIn)
            : null;

        return service;
    }

    internal static dynamic? ExtractTypeFromMemory<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type tIn
    )
    {
        try
        {
            var input = tIn.IsTuple()
                ? ExtractTuple(workflow, tIn)
                : workflow.Memory.GetValueOrDefault(tIn)
                    ?? workflow.ExtractTypeFromServiceProvider(tIn)
                    ?? workflow.ExtractLoggerFromLoggerFactory(tIn);

            if (input is null)
                throw new WorkflowException($"Could not find type: ({tIn}).");

            return input;
        }
        catch (Exception e)
        {
            workflow.Exception ??= e;
            return null;
        }
    }

    internal static dynamic ExtractTuple<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type inputType
    )
    {
        var typeTuples = TypeHelpers.ExtractTypeTuples(workflow.Memory, inputType);

        return typeTuples.Count switch
        {
            0 => throw new WorkflowException($"Cannot have Tuple of length 0."),
            1
                => throw new WorkflowException(
                    "Tuple of a single length should be passed as the value itself."
                ),
            2 => TypeHelpers.ConvertTwoTuple(typeTuples),
            3 => TypeHelpers.ConvertThreeTuple(typeTuples),
            4 => TypeHelpers.ConvertFourTuple(typeTuples),
            5 => TypeHelpers.ConvertFiveTuple(typeTuples),
            6 => TypeHelpers.ConvertSixTuple(typeTuples),
            7 => TypeHelpers.ConvertSevenTuple(typeTuples),
            _ => throw new WorkflowException($"Could not create Tuple for type ({inputType})")
        };
    }

    internal static Unit AddTupleToMemory<TIn, TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        TIn input
    )
    {
        if (!typeof(TIn).IsTuple())
            throw new WorkflowException(
                $"({typeof(TIn)}) is not a Tuple but was attempted to be extracted as one."
            );

        if (input is null)
            throw new WorkflowException($"Input of type ({typeof(TIn)} cannot be null.");

        var inputTuple = (ITuple)input;

        if (inputTuple.Length > 7)
            throw new WorkflowException(
                $"Tuple input ({typeof(TIn)}) cannot have a length greater than 7."
            );

        var tupleList = Enumerable.Range(0, inputTuple.Length).Select(i => inputTuple[i]!).ToList();

        foreach (var tupleValue in tupleList)
            workflow.Memory[tupleValue.GetType()] = tupleValue;

        return Unit.Default;
    }
}
