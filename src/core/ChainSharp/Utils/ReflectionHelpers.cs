using System.Collections.Concurrent;
using System.Reflection;
using ChainSharp.Step;
using ChainSharp.Workflow;
using LanguageExt;

namespace ChainSharp.Utils;

/// <summary>
/// Provides helper methods for working with reflection.
/// These methods are used throughout ChainSharp to dynamically invoke methods and extract type information.
/// Results are cached in static ConcurrentDictionary instances to avoid repeated reflection lookups.
/// </summary>
internal static class ReflectionHelpers
{
    /// <summary>
    /// Cache for step type arguments (TIn, TOut) keyed by the step type.
    /// Since a step's IStep&lt;TIn, TOut&gt; interface never changes, this is safe to cache statically.
    /// </summary>
    private static readonly ConcurrentDictionary<
        Type,
        (Type TIn, Type TOut)
    > StepTypeArgumentsCache = new();

    /// <summary>
    /// Cache for resolved generic MethodInfo instances, keyed by (workflowType, methodName, stepType, tIn, tOut, paramCount).
    /// </summary>
    private static readonly ConcurrentDictionary<
        (Type WorkflowType, string MethodName, Type StepType, Type TIn, Type TOut, int ParamCount),
        MethodInfo
    > GenericMethodCache = new();

    /// <summary>
    /// Extracts the input and output type arguments from an IStep implementation.
    /// Results are cached per step type for subsequent calls.
    /// </summary>
    /// <typeparam name="TStep">The step type</typeparam>
    /// <returns>A tuple containing the input type and output type</returns>
    /// <exception cref="InvalidOperationException">Thrown if TStep does not implement IStep&lt;TIn, TOut&gt;</exception>
    internal static (Type, Type) ExtractStepTypeArguments<TStep>()
    {
        var stepType = typeof(TStep);

        if (StepTypeArgumentsCache.TryGetValue(stepType, out var cached))
            return cached;

        // Find the IStep<,> interface
        var interfaceType = stepType
            .GetInterfaces()
            .FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStep<,>)
            );

        if (interfaceType is null)
        {
            throw new InvalidOperationException(
                $"{nameof(TStep)} does not implement IStep<TIn, TOut>."
            );
        }

        // Extract the generic arguments
        var types = interfaceType.GetGenericArguments();
        var result = (types[0], types[1]);

        StepTypeArgumentsCache.TryAdd(stepType, result);

        return result;
    }

    /// <summary>
    /// Finds the ShortCircuitChain method that matches the specified types and parameter count.
    /// Results are cached per (workflowType, stepType, tIn, tOut, paramCount) combination.
    /// </summary>
    internal static MethodInfo FindGenericChainInternalMethod<TStep, TInput, TReturn>(
        Workflow<TInput, TReturn> workflow,
        Type tIn,
        Type tOut,
        int parameterCount
    )
    {
        var cacheKey = (
            workflow.GetType(),
            "ShortCircuitChain",
            typeof(TStep),
            tIn,
            tOut,
            parameterCount
        );

        if (GenericMethodCache.TryGetValue(cacheKey, out var cached))
            return cached;

        return GenericMethodCache.GetOrAdd(
            cacheKey,
            _ =>
            {
                var methods = workflow
                    .GetType()
                    .GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    )
                    .Where(m => m is { Name: "ShortCircuitChain", IsGenericMethodDefinition: true })
                    .Where(m => m.GetGenericArguments().Length == 3)
                    .Where(m => m.GetParameters().Length == parameterCount)
                    .ToList();

                switch (methods.Count)
                {
                    case > 1:
                        throw new InvalidOperationException(
                            "More than one Generic 'Chain' method found."
                        );
                    case 0:
                        throw new InvalidOperationException("Suitable 'Chain' method not found.");
                }

                return methods.First().MakeGenericMethod(typeof(TStep), tIn, tOut);
            }
        );
    }

    /// <summary>
    /// Finds the Chain method that matches the specified types and parameter count.
    /// Results are cached per (workflowType, stepType, tIn, tOut, paramCount) combination.
    /// </summary>
    internal static MethodInfo FindGenericChainMethod<TStep, TInput, TReturn>(
        Workflow<TInput, TReturn> workflow,
        Type tIn,
        Type tOut,
        int parameterCount
    )
    {
        var cacheKey = (workflow.GetType(), "Chain", typeof(TStep), tIn, tOut, parameterCount);

        if (GenericMethodCache.TryGetValue(cacheKey, out var cached))
            return cached;

        return GenericMethodCache.GetOrAdd(
            cacheKey,
            _ =>
            {
                var methods = workflow
                    .GetType()
                    .GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    )
                    .Where(m => m is { Name: "Chain", IsGenericMethodDefinition: true })
                    .Where(m => m.GetGenericArguments().Length == 3)
                    .Where(m => m.GetParameters().Length == parameterCount)
                    .ToList();

                switch (methods.Count)
                {
                    case > 1:
                        throw new InvalidOperationException(
                            "More than one Generic 'Chain' method found."
                        );
                    case 0:
                        throw new InvalidOperationException("Suitable 'Chain' method not found.");
                }

                return methods.First().MakeGenericMethod(typeof(TStep), tIn, tOut);
            }
        );
    }

    /// <summary>
    /// Extracts the Right value from a dynamic Either object.
    /// </summary>
    /// <param name="eitherObject">The Either object</param>
    /// <returns>An Option containing the Right value, or None if the Either is Left or not an Either</returns>
    /// <remarks>
    /// This method is used by the ShortCircuit method to extract the Right value from an Either
    /// when the types are not known at compile time.
    /// </remarks>
    internal static Option<dynamic> GetRightFromDynamicEither(dynamic eitherObject)
    {
        var eitherType = eitherObject.GetType();

        // Check if the object is an Either
        if (eitherType.IsGenericType && eitherType.GetGenericTypeDefinition() == typeof(Either<,>))
        {
            // Get the IsRight property
            var isRightProp = eitherType.GetProperty("IsRight");
            var isRight = (bool)isRightProp.GetValue(eitherObject, null);

            // If it's Right, get the value
            if (isRight)
            {
                var rightValueProp = eitherType.GetProperty("Case");
                var rightValue = rightValueProp.GetValue(eitherObject, null);
                return rightValue;
            }
        }

        return Option<dynamic>.None;
    }
}
