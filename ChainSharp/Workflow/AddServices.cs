using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using LanguageExt;

namespace ChainSharp.Workflow;

public partial class Workflow<TInput, TReturn>
{
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

    private Workflow<TInput, TReturn> AddServices(object[] services, Type[] typeArray)
    {
        // Always allow input type of Unit for parameterless invocation
        Memory ??= new Dictionary<Type, object>() { { typeof(Unit), Unit.Default } };

        foreach (var service in services)
        {
            var serviceType = service.GetType();

            if (serviceType.IsMoqProxy())
            {
                var mockedType = service.GetMockedTypeFromObject();
                Memory[mockedType] = service;
                continue;
            }

            if (!serviceType.IsClass)
                throw new WorkflowException(
                    $"Params ({serviceType}) to AddServices must be Classes."
                );

            var interfaces = serviceType.GetInterfaces();
            var foundInterface = interfaces.FirstOrDefault(x => typeArray.Contains(x));

            if (foundInterface is null)
                throw new WorkflowException($"Class ({serviceType}) does not have any interfaces.");

            Memory[foundInterface] = service;
        }

        return this;
    }
}
