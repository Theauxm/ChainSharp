using System.Reflection;
using System.Text.Json;
using ChainSharp.Effect.Attributes;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Effect.Utils;
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

        return serviceCollection
            .AddSingleton<IChainSharpEffectConfiguration>(configuration)
            .AddTransient<IEffectRunner, EffectRunner>();
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
        this ChainSharpEffectConfigurationBuilder builder,
        TEffectFactory factory
    )
        where TIEffectFactory : class, IEffectProviderFactory
        where TEffectFactory : class, TIEffectFactory
    {
        builder
            .ServiceCollection.AddSingleton<TIEffectFactory>(factory)
            .AddSingleton<IEffectProviderFactory>(factory);

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TIEffectFactory, TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder
    )
        where TIEffectFactory : class, IEffectProviderFactory
        where TEffectFactory : class, TIEffectFactory
    {
        builder
            .ServiceCollection.AddSingleton<IEffectProviderFactory, TEffectFactory>()
            .AddSingleton<TIEffectFactory, TEffectFactory>();

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder,
        TEffectFactory factory
    )
        where TEffectFactory : class, IEffectProviderFactory
    {
        builder.ServiceCollection.AddSingleton<IEffectProviderFactory>(factory);

        return builder;
    }

    public static ChainSharpEffectConfigurationBuilder AddEffect<TEffectFactory>(
        this ChainSharpEffectConfigurationBuilder builder
    )
        where TEffectFactory : class, IEffectProviderFactory, new()
    {
        var factory = new TEffectFactory();

        return builder.AddEffect(factory);
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
}
