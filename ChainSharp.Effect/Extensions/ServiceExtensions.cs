using System.Reflection;
using ChainSharp.Effect.Attributes;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.Effect;
using ChainSharp.Effect.Services.EffectFactory;
using ChainSharp.Effect.Services.EffectLogger;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddChainSharpEffects(
        this IServiceCollection serviceCollection,
        Action<ChainSharpEffectConfigurationBuilder>? options = null
    )
    {
        var configuration = BuildConfiguration(serviceCollection, options);

        return serviceCollection;
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

    public static ChainSharpEffectConfigurationBuilder AddEffect<TIEffectFactory, TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder
    )
        where TIEffectFactory : class, IEffectFactory
        where TEffectFactory : class, TIEffectFactory, new()
    {
        var effectFactory = new TEffectFactory();

        return builder.AddEffect<TIEffectFactory, TEffectFactory>(effectFactory);
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TIEffectFactory, TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder,
        TEffectFactory factory
    )
        where TIEffectFactory : class, IEffectFactory
        where TEffectFactory : class, TIEffectFactory
    {
        builder
            .ServiceCollection.AddSingleton<TIEffectFactory>(factory)
            .AddSingleton<IEffectFactory>(factory);

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder,
        TEffectFactory factory
    )
        where TEffectFactory : class, IEffectFactory
    {
        builder.ServiceCollection.AddSingleton<IEffectFactory>(factory);

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder
    )
        where TEffectFactory : class, IEffectFactory, new()
    {
        var factory = new TEffectFactory();

        return builder.AddEffect(factory);
    }

    public static ChainSharpEffectConfigurationBuilder AddConsoleLogger(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    ) => configurationBuilder.AddCustomLogger<EffectLogger>();

    public static ChainSharpEffectConfigurationBuilder AddCustomLogger<TWorkflowLogger>(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    )
        where TWorkflowLogger : class, IEffectLogger
    {
        configurationBuilder.ServiceCollection.AddScoped<IEffectLogger, TWorkflowLogger>();

        return configurationBuilder;
    }

    public static void InjectProperties(this IServiceProvider serviceProvider, object instance)
    {
        var properties = instance
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(InjectAttribute)) && p.CanWrite);

        foreach (var property in properties)
        {
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

    public static IServiceCollection AddChainSharpWorkflow<TService>(
        this IServiceCollection services
    )
        where TService : class
    {
        services.AddScoped<TService>(sp =>
        {
            var instance = ActivatorUtilities.CreateInstance<TService>(sp);
            sp.InjectProperties(instance);
            return instance;
        });

        return services;
    }
}
