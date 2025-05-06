using System.Reflection;
using ChainSharp.Step;
using ChainSharp.Workflow;
using LanguageExt;

namespace ChainSharp.Utils;

/// <summary>
/// Provides helper methods for working with reflection.
/// These methods are used throughout ChainSharp to dynamically invoke methods and extract type information.
/// </summary>
internal static class ReflectionHelpers
{
    /// <summary>
    /// Extracts the input and output type arguments from an IStep implementation.
    /// </summary>
    /// <typeparam name="TStep">The step type</typeparam>
    /// <returns>A tuple containing the input type and output type</returns>
    /// <exception cref="InvalidOperationException">Thrown if TStep does not implement IStep&lt;TIn, TOut&gt;</exception>
    /// <remarks>
    /// This method is used to determine the input and output types of a step at runtime.
    /// It's used by the Chain and ShortCircuit methods to find the appropriate method to call.
    /// </remarks>
    internal static (Type, Type) ExtractStepTypeArguments<TStep>()
    {
        var stepType = typeof(TStep);
        
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
        var tIn = types[0];
        var tOut = types[1];

        return (tIn, tOut);
    }

    /// <summary>
    /// Finds the ShortCircuitChain method that matches the specified types and parameter count.
    /// </summary>
    /// <typeparam name="TStep">The step type</typeparam>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="tIn">The step input type</param>
    /// <param name="tOut">The step output type</param>
    /// <param name="parameterCount">The number of parameters</param>
    /// <returns>The method info for the matching method</returns>
    /// <exception cref="InvalidOperationException">Thrown if no matching method is found or if multiple matching methods are found</exception>
    /// <remarks>
    /// This method is used by the ShortCircuit method to find the appropriate ShortCircuitChain method to call.
    /// It searches for a method with the specified name, generic argument count, and parameter count.
    /// </remarks>
    internal static MethodInfo FindGenericChainInternalMethod<TStep, TInput, TReturn>(
        Workflow<TInput, TReturn> workflow,
        Type tIn,
        Type tOut,
        int parameterCount
    )
    {
        // Find a Generic Chain with 3 Type arguments and parameterCount
        var methods = workflow
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m is { Name: "ShortCircuitChain", IsGenericMethodDefinition: true })
            .Where(m => m.GetGenericArguments().Length == 3)
            .Where(m => m.GetParameters().Length == parameterCount)
            .ToList();

        switch (methods.Count)
        {
            case > 1:
                throw new InvalidOperationException("More than one Generic 'Chain' method found.");
            case 0:
                throw new InvalidOperationException("Suitable 'Chain' method not found.");
        }

        // Create a generic method with the specified types
        var method = methods.First();
        var genericMethod = method.MakeGenericMethod(typeof(TStep), tIn, tOut);
        return genericMethod;
    }

    /// <summary>
    /// Finds the Chain method that matches the specified types and parameter count.
    /// </summary>
    /// <typeparam name="TStep">The step type</typeparam>
    /// <typeparam name="TInput">The workflow input type</typeparam>
    /// <typeparam name="TReturn">The workflow return type</typeparam>
    /// <param name="workflow">The workflow instance</param>
    /// <param name="tIn">The step input type</param>
    /// <param name="tOut">The step output type</param>
    /// <param name="parameterCount">The number of parameters</param>
    /// <returns>The method info for the matching method</returns>
    /// <exception cref="InvalidOperationException">Thrown if no matching method is found or if multiple matching methods are found</exception>
    /// <remarks>
    /// This method is used by the Chain method to find the appropriate Chain method to call.
    /// It searches for a method with the specified name, generic argument count, and parameter count.
    /// </remarks>
    internal static MethodInfo FindGenericChainMethod<TStep, TInput, TReturn>(
        Workflow<TInput, TReturn> workflow,
        Type tIn,
        Type tOut,
        int parameterCount
    )
    {
        // Find a Generic Chain with 3 Type arguments and parameterCount
        var methods = workflow
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m is { Name: "Chain", IsGenericMethodDefinition: true })
            .Where(m => m.GetGenericArguments().Length == 3)
            .Where(m => m.GetParameters().Length == parameterCount)
            .ToList();

        switch (methods.Count)
        {
            case > 1:
                throw new InvalidOperationException("More than one Generic 'Chain' method found.");
            case 0:
                throw new InvalidOperationException("Suitable 'Chain' method not found.");
        }

        // Create a generic method with the specified types
        var method = methods.First();
        var genericMethod = method.MakeGenericMethod(typeof(TStep), tIn, tOut);
        return genericMethod;
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
