using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Json.Services.JsonEffect;
using ChainSharp.Effect.Json.Services.JsonEffectFactory;
using ChainSharp.Effect.Services.EffectProviderFactory;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Json.Extensions;

/// <summary>
/// Provides extension methods for configuring ChainSharp.Effect.Json services in the dependency injection container.
/// </summary>
/// <remarks>
/// The ServiceExtensions class contains utility methods that simplify the registration
/// of ChainSharp.Effect.Json services with the dependency injection system.
///
/// These extensions enable JSON serialization support for the ChainSharp.Effect system,
/// allowing workflow models to be serialized to and from JSON format.
///
/// By using these extensions, applications can easily configure and use the
/// ChainSharp.Effect.Json system with minimal boilerplate code.
/// </remarks>
public static class ServiceExtensions
{
    /// <summary>
    /// Adds JSON effect support to the ChainSharp effect configuration builder.
    /// </summary>
    /// <param name="configurationBuilder">The ChainSharp effect configuration builder</param>
    /// <returns>The configuration builder for method chaining</returns>
    /// <remarks>
    /// This method configures the ChainSharp.Effect system to use JSON serialization for
    /// tracking and logging model changes. It registers the necessary services with the
    /// dependency injection container.
    ///
    /// The method performs the following steps:
    /// 1. Registers the JsonEffectProvider as a transient service
    /// 2. Registers the JsonEffectProviderFactory as an IEffectProviderFactory
    ///
    /// This enables the ChainSharp.Effect system to track model changes and serialize them
    /// to JSON format for logging or persistence.
    ///
    /// Example usage:
    /// ```csharp
    /// services.AddChainSharpEffects(options =>
    ///     options.AddJsonEffect()
    /// );
    /// ```
    /// </remarks>
    public static ChainSharpEffectConfigurationBuilder AddJsonEffect(
        this ChainSharpEffectConfigurationBuilder configurationBuilder
    )
    {
        configurationBuilder.ServiceCollection.AddTransient<
            IJsonEffectProvider,
            JsonEffectProvider
        >();

        return configurationBuilder.AddEffect<JsonEffectProviderFactory>();
    }
}
