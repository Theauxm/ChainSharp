using System.Reflection;
using ChainSharp.Exceptions;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Extensions;

public static class LoggerExtensions
{
    public static dynamic CreateGenericLogger(this ILoggerFactory loggerFactory, Type genericType)
    {
        // 1. Find the generic CreateLogger<T> method
        var genericMethod = typeof(LoggerFactoryExtensions)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(m => m.Name == nameof(LoggerFactoryExtensions.CreateLogger) &&
                                 m.GetParameters().Length == 1 && // Only one parameter (ILoggerFactory)
                                 m.IsGenericMethod);

        if (genericMethod is null)
            throw new WorkflowException(
                $"Could not find Generic method on LoggerFactoryExtensions for CreateLogger<({genericType.Name})>.");

        // 2. Make the method generic with the desired type
        var specificMethod = genericMethod.MakeGenericMethod(genericType);

        if (specificMethod is null)
            throw new WorkflowException($"Could not create generic method CreateLogger<({genericType.Name})> on LoggerFactoryExtensions.");

        // 3. Invoke the method
        var loggerInstance = specificMethod.Invoke(null, [loggerFactory]);
        
        if (loggerInstance is null)
            throw new WorkflowException($"Could not create generic logger CreateLogger<({genericType.Name})>.");

        return loggerInstance;
    }

    public static List<ILoggerProvider> GetLoggerProviders(this ILoggerFactory loggerFactory)
    {
        var field = loggerFactory.GetType()
            .GetField("_providers", BindingFlags.NonPublic | BindingFlags.Instance);

        return field?.GetValue(loggerFactory) as List<ILoggerProvider> ?? [];
    }
}