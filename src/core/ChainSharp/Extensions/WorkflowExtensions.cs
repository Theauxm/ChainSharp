using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using ChainSharp.Exceptions;
using ChainSharp.Utils;
using ChainSharp.Workflow;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Extensions;

/// <summary>
/// Provides extension methods for working with workflows.
/// These methods enable dependency injection, type extraction, and tuple handling in workflows.
/// </summary>
public static class WorkflowExtensions
{
    /// <summary>
    /// Cache for step constructor info and parameter types, keyed by step type.
    /// </summary>
    private static readonly ConcurrentDictionary<
        Type,
        (ConstructorInfo Constructor, Type[] ParameterTypes)?
    > ConstructorCache = new();

    /// <summary>
    /// Initializes a step instance by extracting its constructor parameters from Memory.
    /// Constructor metadata is cached per step type for subsequent calls.
    /// </summary>
    /// <typeparam name="TStep">The type of step to initialize</typeparam>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <returns>The initialized step instance, or null if initialization fails</returns>
    public static TStep? InitializeStep<TStep, TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow
    )
        where TStep : class
    {
        var stepType = typeof(TStep);

        var cached = ConstructorCache.GetOrAdd(
            stepType,
            type =>
            {
                if (!type.IsClass)
                    return null;

                var constructors = type.GetConstructors();
                if (constructors.Length != 1)
                    return null;

                var parameterTypes = constructors[0]
                    .GetParameters()
                    .Select(x => x.ParameterType)
                    .ToArray();

                return (constructors[0], parameterTypes);
            }
        );

        if (cached is null)
        {
            // Determine the specific error for the caller
            if (!stepType.IsClass)
                workflow.Exception ??= new WorkflowException($"Step ({stepType}) must be a class.");
            else
                workflow.Exception ??= new WorkflowException(
                    $"Step classes can only have a single constructor ({stepType})."
                );
            return null;
        }

        var (constructor, constructorArguments) = cached.Value;

        // Extract the constructor parameters from Memory
        var constructorParameters = ExtractTypesFromMemory(workflow, constructorArguments);

        if (workflow.Exception is not null)
            return null;

        // Create an instance of the step
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

    /// <summary>
    /// Extracts multiple types from Memory.
    /// </summary>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="types">The types to extract</param>
    /// <returns>An array of extracted values</returns>
    /// <remarks>
    /// This method is used to extract multiple types from Memory at once.
    /// It's used by InitializeStep to extract constructor parameters.
    /// </remarks>
    public static dynamic?[] ExtractTypesFromMemory<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        IEnumerable<Type> types
    )
    {
        var typeArray = types as Type[] ?? types.ToArray();
        var result = new dynamic?[typeArray.Length];
        for (var i = 0; i < typeArray.Length; i++)
            result[i] = ExtractTypeFromMemory(workflow, typeArray[i]);
        return result;
    }

    /// <summary>
    /// Extracts a value of type T from Memory.
    /// </summary>
    /// <typeparam name="T">The type to extract</typeparam>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <returns>The extracted value, or default if not found</returns>
    /// <remarks>
    /// This method is a strongly-typed wrapper around ExtractTypeFromMemory.
    /// It's used by Chain and other methods to extract values from Memory.
    /// </remarks>
    public static T? ExtractTypeFromMemory<T, TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow
    )
    {
        var type = workflow.ExtractTypeFromMemory(typeof(T));

        return type is null ? default : (T)type;
    }

    /// <summary>
    /// Extracts a logger from a logger factory.
    /// </summary>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="tIn">The logger type</param>
    /// <returns>The logger, or null if not applicable</returns>
    /// <exception cref="WorkflowException">Thrown if the logger factory is not found or if the generic arguments are invalid</exception>
    /// <remarks>
    /// This method is used to create loggers dynamically at runtime.
    /// It's used by ExtractTypeFromMemory to handle ILogger&lt;T&gt; types.
    /// </remarks>
    internal static dynamic? ExtractLoggerFromLoggerFactory<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type tIn
    )
    {
        // Check if the type is ILogger<T>
        if (tIn.IsGenericType == false || tIn.GetGenericTypeDefinition() != typeof(ILogger<>))
            return null;

        // Get the logger factory from Memory
        if (
            workflow.Memory.GetValueOrDefault(typeof(ILoggerFactory))
            is not ILoggerFactory loggerFactory
        )
            throw new WorkflowException(
                $"Could not find ILoggerFactory for input type: ({tIn}). Have you injected an ILoggerFactory into the Workflow's services?"
            );

        var generics = tIn.GetGenericArguments();

        // Ensure there's exactly one generic argument
        if (generics.Length != 1)
            throw new WorkflowException(
                $"Incorrect number of generic arguments for input type ({tIn}). Found ({generics.Length}) generics with types ({string.Join(", ", generics.Select(x => x.Name))})."
            );

        // Create a logger for the generic type
        return loggerFactory.CreateGenericLogger(generics.First());
    }

    /// <summary>
    /// Extracts a service from a service provider.
    /// </summary>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="tIn">The service type</param>
    /// <returns>The service, or null if not found</returns>
    /// <remarks>
    /// This method is used to resolve services from a service provider.
    /// It's used by ExtractTypeFromMemory to handle dependency injection.
    /// </remarks>
    internal static dynamic? ExtractTypeFromServiceProvider<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type tIn
    )
    {
        // Get the service provider from Memory
        var service = workflow.Memory.GetValueOrDefault(typeof(IServiceProvider))
            is IServiceProvider serviceProvider
            ? serviceProvider.GetService(tIn)
            : null;

        return service;
    }

    /// <summary>
    /// Extracts a value from Memory by its type.
    /// </summary>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="tIn">The type to extract</param>
    /// <returns>The extracted value, or null if not found</returns>
    /// <exception cref="WorkflowException">Thrown if the type is not found</exception>
    /// <remarks>
    /// This method is the core type extraction method used throughout ChainSharp.
    /// It tries multiple sources in the following order:
    /// 1. If the type is a tuple, extract it using ExtractTuple
    /// 2. Otherwise, try to get it directly from Memory
    /// 3. If not found, try to get it from a service provider
    /// 4. If not found, try to get it from a logger factory
    /// 5. If still not found, throw an exception
    /// </remarks>
    public static dynamic? ExtractTypeFromMemory<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type tIn
    )
    {
        try
        {
            // Try multiple sources in order
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

    /// <summary>
    /// Extracts a tuple from Memory.
    /// </summary>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="inputType">The tuple type</param>
    /// <returns>The extracted tuple</returns>
    /// <exception cref="WorkflowException">Thrown if the tuple cannot be created</exception>
    /// <remarks>
    /// This method extracts the components of a tuple from Memory and creates a new tuple.
    /// It's used by ExtractTypeFromMemory to handle tuple types.
    /// </remarks>
    public static dynamic ExtractTuple<TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        Type inputType
    )
    {
        // Extract the tuple components from Memory
        var typeTuples = TypeHelpers.ExtractTypeTuples(workflow.Memory, inputType);

        // Create a tuple of the appropriate size
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

    /// <summary>
    /// Adds a tuple to Memory by extracting its components.
    /// </summary>
    /// <typeparam name="TIn">The tuple type</typeparam>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="input">The tuple to add</param>
    /// <returns>Unit.Default</returns>
    /// <exception cref="WorkflowException">Thrown if the input is not a tuple, is null, or has more than 7 elements</exception>
    /// <remarks>
    /// This method extracts the components of a tuple and adds them to Memory individually.
    /// It's used by Chain and other methods to handle tuple results.
    /// </remarks>
    public static Unit AddTupleToMemory<TIn, TInput, TReturn>(
        this Workflow<TInput, TReturn> workflow,
        TIn input
    )
    {
        // Verify that TIn is a tuple type
        if (!typeof(TIn).IsTuple())
            throw new WorkflowException(
                $"({typeof(TIn)}) is not a Tuple but was attempted to be extracted as one."
            );

        // Verify that input is not null
        if (input is null)
            throw new WorkflowException($"Input of type ({typeof(TIn)} cannot be null.");

        var inputTuple = (ITuple)input;

        // Verify that the tuple has at most 7 elements
        if (inputTuple.Length > 7)
            throw new WorkflowException(
                $"Tuple input ({typeof(TIn)}) cannot have a length greater than 7."
            );

        // Extract the tuple elements
        var tupleList = Enumerable.Range(0, inputTuple.Length).Select(i => inputTuple[i]!).ToList();

        // Add each element to Memory
        foreach (var tupleValue in tupleList)
        {
            var tupleValueType = tupleValue.GetType();

            // Add by concrete type
            workflow.Memory[tupleValueType] = tupleValue;

            // Add by interfaces
            var tupleValueTypeInterfaces = tupleValueType.GetInterfaces();
            foreach (var tupleValueTypeInterface in tupleValueTypeInterfaces)
                workflow.Memory[tupleValueTypeInterface] = tupleValue;
        }

        return Unit.Default;
    }
}
