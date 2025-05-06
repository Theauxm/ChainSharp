using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
    /// <summary>
    /// Adds a service to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the service</typeparam>
    /// <param name="service">The service instance to add</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// Services are stored in the workflow's memory and can be retrieved by steps
    /// that need them. This enables dependency injection within workflows.
    /// </remarks>
    public Workflow<TInput, TReturn> AddServices<T1>(T1 service)
    {
        Type[] typeArray = [typeof(T1)];

        return AddServices([service], typeArray);
    }

    /// <summary>
    /// Adds two services to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the first service</typeparam>
    /// <typeparam name="T2">The type of the second service</typeparam>
    /// <param name="service1">The first service instance</param>
    /// <param name="service2">The second service instance</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> AddServices<T1, T2>(T1 service1, T2 service2)
    {
        Type[] typeArray = [typeof(T1), typeof(T2)];
        object[] services = [service1, service2];

        return AddServices(services, typeArray);
    }

    /// <summary>
    /// Adds three services to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the first service</typeparam>
    /// <typeparam name="T2">The type of the second service</typeparam>
    /// <typeparam name="T3">The type of the third service</typeparam>
    /// <param name="service1">The first service instance</param>
    /// <param name="service2">The second service instance</param>
    /// <param name="service3">The third service instance</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> AddServices<T1, T2, T3>(T1 service1, T2 service2, T3 service3)
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3)];
        object[] services = [service1, service2, service3];

        return AddServices(services, typeArray);
    }

    /// <summary>
    /// Adds four services to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the first service</typeparam>
    /// <typeparam name="T2">The type of the second service</typeparam>
    /// <typeparam name="T3">The type of the third service</typeparam>
    /// <typeparam name="T4">The type of the fourth service</typeparam>
    /// <param name="service1">The first service instance</param>
    /// <param name="service2">The second service instance</param>
    /// <param name="service3">The third service instance</param>
    /// <param name="service4">The fourth service instance</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4>(
        T1 service1,
        T2 service2,
        T3 service3,
        T4 service4
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4)];
        object[] services = [service1, service2, service3, service4];

        return AddServices(services, typeArray);
    }

    /// <summary>
    /// Adds five services to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the first service</typeparam>
    /// <typeparam name="T2">The type of the second service</typeparam>
    /// <typeparam name="T3">The type of the third service</typeparam>
    /// <typeparam name="T4">The type of the fourth service</typeparam>
    /// <typeparam name="T5">The type of the fifth service</typeparam>
    /// <param name="service1">The first service instance</param>
    /// <param name="service2">The second service instance</param>
    /// <param name="service3">The third service instance</param>
    /// <param name="service4">The fourth service instance</param>
    /// <param name="service5">The fifth service instance</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4, T5>(
        T1 service1,
        T2 service2,
        T3 service3,
        T4 service4,
        T5 service5
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)];
        object[] services = [service1, service2, service3, service4, service5];

        return AddServices(services, typeArray);
    }

    /// <summary>
    /// Adds six services to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the first service</typeparam>
    /// <typeparam name="T2">The type of the second service</typeparam>
    /// <typeparam name="T3">The type of the third service</typeparam>
    /// <typeparam name="T4">The type of the fourth service</typeparam>
    /// <typeparam name="T5">The type of the fifth service</typeparam>
    /// <typeparam name="T6">The type of the sixth service</typeparam>
    /// <param name="service1">The first service instance</param>
    /// <param name="service2">The second service instance</param>
    /// <param name="service3">The third service instance</param>
    /// <param name="service4">The fourth service instance</param>
    /// <param name="service5">The fifth service instance</param>
    /// <param name="service6">The sixth service instance</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4, T5, T6>(
        T1 service1,
        T2 service2,
        T3 service3,
        T4 service4,
        T5 service5,
        T6 service6
    )
    {
        Type[] typeArray = [typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)];
        object[] services = [service1, service2, service3, service4, service5, service6];

        return AddServices(services, typeArray);
    }

    /// <summary>
    /// Adds seven services to the workflow's memory.
    /// </summary>
    /// <typeparam name="T1">The type of the first service</typeparam>
    /// <typeparam name="T2">The type of the second service</typeparam>
    /// <typeparam name="T3">The type of the third service</typeparam>
    /// <typeparam name="T4">The type of the fourth service</typeparam>
    /// <typeparam name="T5">The type of the fifth service</typeparam>
    /// <typeparam name="T6">The type of the sixth service</typeparam>
    /// <typeparam name="T7">The type of the seventh service</typeparam>
    /// <param name="service1">The first service instance</param>
    /// <param name="service2">The second service instance</param>
    /// <param name="service3">The third service instance</param>
    /// <param name="service4">The fourth service instance</param>
    /// <param name="service5">The fifth service instance</param>
    /// <param name="service6">The sixth service instance</param>
    /// <param name="service7">The seventh service instance</param>
    /// <returns>The workflow instance for method chaining</returns>
    public Workflow<TInput, TReturn> AddServices<T1, T2, T3, T4, T5, T6, T7>(
        T1 service1,
        T2 service2,
        T3 service3,
        T4 service4,
        T5 service5,
        T6 service6,
        T7 service7
    )
    {
        Type[] typeArray =
        [
            typeof(T1),
            typeof(T2),
            typeof(T3),
            typeof(T4),
            typeof(T5),
            typeof(T6),
            typeof(T7)
        ];
        object[] services = [service1, service2, service3, service4, service5, service6, service7];

        return AddServices(services, typeArray);
    }

    /// <summary>
    /// Internal method that adds services to the workflow's memory.
    /// </summary>
    /// <param name="services">The service instances to add</param>
    /// <param name="typeArray">The types of the services</param>
    /// <returns>The workflow instance for method chaining</returns>
    /// <remarks>
    /// This method handles the actual storage of services in the Memory dictionary.
    /// It has special handling for Moq mock objects, storing them by their mocked interface type.
    /// For regular services, it stores them by their interface type.
    /// </remarks>
    internal Workflow<TInput, TReturn> AddServices(object[] services, Type[] typeArray)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() { { typeof(Unit), Unit.Default } };

        foreach (var service in services)
        {
            var serviceType = service.GetType();

            // Special handling for Moq mock objects
            if (serviceType.IsMoqProxy())
            {
                var mockedType = service.GetMockedTypeFromObject();
                Memory[mockedType] = service;
                continue;
            }

            // Services must be classes
            if (!serviceType.IsClass)
            {
                Exception ??= new WorkflowException(
                    $"Params ({serviceType}) to AddServices must be Classes."
                );
                return this;
            }

            // Find the interface that matches the type parameter
            var interfaces = serviceType.GetInterfaces();
            var foundInterface = interfaces.FirstOrDefault(typeArray.Contains);

            if (foundInterface is null)
            {
                Exception ??= new WorkflowException(
                    $"Class ({serviceType}) does not have any interfaces."
                );
                return this;
            }

            // Store the service by its interface type
            Memory[foundInterface] = service;
        }

        return this;
    }
}
