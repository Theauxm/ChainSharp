using System.Reflection;

namespace ChainSharp.Utils;

internal static class ReflectionHelpers
{
    /// <summary>
    /// Given a well-formed IStep implementation, extract the two Type arguments
    /// TIn and TOut from the interface.
    /// </summary>
    /// <typeparam name="TStep"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static (Type, Type) ExtractStepTypeArguments<TStep>()
    {
        var stepType = typeof(TStep);
        var interfaceType = stepType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStep<,>));
        if (interfaceType == null)
        {
            throw new InvalidOperationException($"{nameof(TStep)} does not implement IStep<TIn, TOut>.");
        }

        var types = interfaceType.GetGenericArguments();
        var tIn = types[0];
        var tOut = types[1];

        return (tIn, tOut);
    }

    /// <summary>
    /// Given the provided TIn and TOut type arguments, find a Chain method
    /// implementation that can be invoked from the Workflow. Instantiate (prime?)
    /// the method with the TIn and TOut arguments for actual invocation.
    /// </summary>
    /// <param name="workflow"></param>
    /// <param name="tIn"></param>
    /// <param name="tOut"></param>
    /// <typeparam name="TStep"></typeparam>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TReturn"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static MethodInfo FindGenericChainMethod<TStep, TInput, TReturn>(Workflow<TInput, TReturn> workflow,
        Type tIn, Type tOut)
    {
        // Find a Generic Chain with 3 Type arguments and 1 Parameter
        var methods = workflow.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m is { Name: "Chain", IsGenericMethodDefinition: true })
            .Where(m => m.GetGenericArguments().Length == 3)
            .Where(m => m.GetParameters().Length == 1)
            .ToList();

        switch (methods.Count)
        {
            case > 1:
                throw new InvalidOperationException("More than one Generic 'Chain' method found.");
            case 0:
                throw new InvalidOperationException("Suitable 'Chain' method not found.");
        }

        var method = methods.First();
        var genericMethod = method.MakeGenericMethod(typeof(TStep), tIn, tOut);
        return genericMethod;
    }
}