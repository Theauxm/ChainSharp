using System.Reflection;
using ChainSharp.Effect.Attributes;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Effect.Services.StepEffectProviderFactory;
using ChainSharp.Effect.Services.StepEffectRunner;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Extensions;

public static class ServiceExtensions
{
    #region Configuration

    public static IServiceCollection AddChainSharpEffects(
        this IServiceCollection serviceCollection,
        Action<ChainSharpEffectConfigurationBuilder>? options = null
    )
    {
        var configuration = BuildConfiguration(serviceCollection, options);

        return serviceCollection
            .AddSingleton<IChainSharpEffectConfiguration>(configuration)
            .AddTransient<IEffectRunner, EffectRunner>()
            .AddTransient<IStepEffectRunner, StepEffectRunner>();
    }

    private static ChainSharpEffectConfiguration BuildConfiguration(
        IServiceCollection serviceCollection,
        Action<ChainSharpEffectConfigurationBuilder>? options
    )
    {
        // Create Builder to be used after Options are invoked
        var builder = new ChainSharpEffectConfigurationBuilder(serviceCollection);

        // Options able to be null since all values have defaults
        options?.Invoke(builder);

        return builder.Build();
    }

    public static void InjectProperties(this IServiceProvider serviceProvider, object instance)
    {
        var properties = instance
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(InjectAttribute)) && p.CanWrite);

        foreach (var property in properties)
        {
            if (property.GetValue(instance) != null)
                continue;

            var propertyType = property.PropertyType;
            object? service = null;

            // Handle IEnumerable<T>
            if (
                propertyType.IsGenericType
                && propertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            )
            {
                var serviceType = propertyType.GetGenericArguments()[0];
                var serviceCollectionType = typeof(IEnumerable<>).MakeGenericType(serviceType);
                service = serviceProvider.GetService(serviceCollectionType);
            }
            else
            {
                service = serviceProvider.GetService(propertyType);
            }

            if (service != null)
            {
                property.SetValue(instance, service);
            }
        }
    }

    #endregion

    #region Effect

    public static ChainSharpEffectConfigurationBuilder AddEffect<
        TIEffectProviderFactory,
        TEffectProviderFactory
    >(this ChainSharpEffectConfigurationBuilder builder, TEffectProviderFactory factory)
        where TIEffectProviderFactory : class, IEffectProviderFactory
        where TEffectProviderFactory : class, TIEffectProviderFactory
    {
        builder
            .ServiceCollection.AddSingleton<TEffectProviderFactory>(factory)
            .AddSingleton<IEffectProviderFactory>(
                sp => sp.GetRequiredService<TEffectProviderFactory>()
            )
            .AddSingleton<TIEffectProviderFactory>(
                sp => sp.GetRequiredService<TEffectProviderFactory>()
            );

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TEffectProviderFactory>(
        this ChainSharpEffectConfigurationBuilder builder
    )
        where TEffectProviderFactory : class, IEffectProviderFactory
    {
        builder
            .ServiceCollection.AddSingleton<TEffectProviderFactory>()
            .AddSingleton<IEffectProviderFactory>(
                sp => sp.GetRequiredService<TEffectProviderFactory>()
            );

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<
        TIEffectProviderFactory,
        TEffectProviderFactory
    >(this ChainSharpEffectConfigurationBuilder builder)
        where TIEffectProviderFactory : class, IEffectProviderFactory
        where TEffectProviderFactory : class, TIEffectProviderFactory
    {
        builder
            .ServiceCollection.AddSingleton<TEffectProviderFactory>()
            .AddSingleton<IEffectProviderFactory>(
                sp => sp.GetRequiredService<TEffectProviderFactory>()
            )
            .AddSingleton<TIEffectProviderFactory>(
                sp => sp.GetRequiredService<TEffectProviderFactory>()
            );

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TEffectProviderFactory>(
        this ChainSharpEffectConfigurationBuilder builder,
        TEffectProviderFactory factory
    )
        where TEffectProviderFactory : class, IEffectProviderFactory
    {
        builder.ServiceCollection.AddSingleton<IEffectProviderFactory>(factory);

        return builder;
    }

    #endregion

    #region StepEffect

    public static ChainSharpEffectConfigurationBuilder AddStepEffect<
        TIStepEffectProviderFactory,
        TStepEffectProviderFactory
    >(this ChainSharpEffectConfigurationBuilder builder, TStepEffectProviderFactory factory)
        where TIStepEffectProviderFactory : class, IStepEffectProviderFactory
        where TStepEffectProviderFactory : class, TIStepEffectProviderFactory
    {
        builder
            .ServiceCollection.AddSingleton<TStepEffectProviderFactory>(factory)
            .AddSingleton<IStepEffectProviderFactory>(
                sp => sp.GetRequiredService<TStepEffectProviderFactory>()
            )
            .AddSingleton<TIStepEffectProviderFactory>(
                sp => sp.GetRequiredService<TStepEffectProviderFactory>()
            );

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddStepEffect<TStepEffectProviderFactory>(
        this ChainSharpEffectConfigurationBuilder builder
    )
        where TStepEffectProviderFactory : class, IStepEffectProviderFactory
    {
        builder
            .ServiceCollection.AddSingleton<TStepEffectProviderFactory>()
            .AddSingleton<IStepEffectProviderFactory>(
                sp => sp.GetRequiredService<TStepEffectProviderFactory>()
            );

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddStepEffect<TStepEffectProviderFactory>(
        this ChainSharpEffectConfigurationBuilder builder,
        TStepEffectProviderFactory factory
    )
        where TStepEffectProviderFactory : class, IStepEffectProviderFactory
    {
        builder.ServiceCollection.AddSingleton<IStepEffectProviderFactory>(factory);

        return builder;
    }

    #endregion

    #region StepInjection

    public static IServiceCollection AddScopedChainSharpStep<TService, TImplementation>(
        this IServiceCollection services
    )
        where TService : class
        where TImplementation : class, TService
        // Nothing inherently different about the injection. Overload for posterity.
        =>
        services.AddScopedChainSharpWorkflow<TService, TImplementation>();

    public static IServiceCollection AddScopedChainSharpStep(
        this IServiceCollection services,
        Type serviceInterface,
        Type serviceImplementation
    )
        // Nothing inherently different about the injection. Overload for posterity.
        =>
        services.AddScopedChainSharpWorkflow(serviceInterface, serviceImplementation);

    public static IServiceCollection AddTransientChainSharpStep<TService, TImplementation>(
        this IServiceCollection services
    )
        where TService : class
        where TImplementation : class, TService
        // Nothing inherently different about the injection. Overload for posterity.
        =>
        services.AddTransientChainSharpWorkflow<TService, TImplementation>();

    public static IServiceCollection AddTransientChainSharpStep(
        this IServiceCollection services,
        Type serviceInterface,
        Type serviceImplementation
    )
        // Nothing inherently different about the injection. Overload for posterity.
        =>
        services.AddTransientChainSharpWorkflow(serviceInterface, serviceImplementation);

    public static IServiceCollection AddSingletonChainSharpStep<TService, TImplementation>(
        this IServiceCollection services
    )
        where TService : class
        where TImplementation : class, TService
        // Nothing inherently different about the injection. Overload for posterity.
        =>
        services.AddSingletonChainSharpWorkflow<TService, TImplementation>();

    public static IServiceCollection AddSingletonChainSharpStep(
        this IServiceCollection services,
        Type serviceInterface,
        Type serviceImplementation
    )
        // Nothing inherently different about the injection. Overload for posterity.
        =>
        services.AddSingletonChainSharpWorkflow(serviceInterface, serviceImplementation);

    #endregion

    #region WorkflowInjection

    public static IServiceCollection AddScopedChainSharpWorkflow<TService, TImplementation>(
        this IServiceCollection services
    )
        where TService : class
        where TImplementation : class, TService
    {
        services.AddScoped<TImplementation>();
        services.AddScoped<TService>(sp =>
        {
            var instance = sp.GetRequiredService<TImplementation>();
            sp.InjectProperties(instance);
            return instance;
        });

        return services;
    }

    public static IServiceCollection AddScopedChainSharpWorkflow(
        this IServiceCollection services,
        Type serviceInterface,
        Type serviceImplementation
    )
    {
        services.AddScoped(serviceImplementation);
        services.AddScoped(
            serviceInterface,
            sp =>
            {
                var instance = sp.GetRequiredService(serviceImplementation);
                sp.InjectProperties(instance);
                return instance;
            }
        );

        return services;
    }

    public static IServiceCollection AddTransientChainSharpWorkflow<TService, TImplementation>(
        this IServiceCollection services
    )
        where TService : class
        where TImplementation : class, TService
    {
        services.AddTransient<TImplementation>();
        services.AddTransient<TService>(sp =>
        {
            var instance = sp.GetRequiredService<TImplementation>();
            sp.InjectProperties(instance);
            return instance;
        });

        return services;
    }

    public static IServiceCollection AddTransientChainSharpWorkflow(
        this IServiceCollection services,
        Type serviceInterface,
        Type serviceImplementation
    )
    {
        services.AddTransient(serviceImplementation);
        services.AddTransient(
            serviceInterface,
            sp =>
            {
                var instance = sp.GetRequiredService(serviceImplementation);
                sp.InjectProperties(instance);
                return instance;
            }
        );

        return services;
    }

    public static IServiceCollection AddSingletonChainSharpWorkflow<TService, TImplementation>(
        this IServiceCollection services
    )
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<TService>(sp =>
        {
            var instance = sp.GetRequiredService<TImplementation>();
            sp.InjectProperties(instance);
            return instance;
        });

        return services;
    }

    public static IServiceCollection AddSingletonChainSharpWorkflow(
        this IServiceCollection services,
        Type serviceInterface,
        Type serviceImplementation
    )
    {
        services.AddSingleton(serviceImplementation);
        services.AddSingleton(
            serviceInterface,
            sp =>
            {
                var instance = sp.GetRequiredService(serviceImplementation);
                sp.InjectProperties(instance);
                return instance;
            }
        );

        return services;
    }

    #endregion
}
