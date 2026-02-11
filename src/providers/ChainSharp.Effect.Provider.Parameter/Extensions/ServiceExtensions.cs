using System.Text.Json;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Provider.Parameter.Services.ParameterEffectProviderFactory;
using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Utils;

namespace ChainSharp.Effect.Provider.Parameter.Extensions;

/// <summary>
/// Provides extension methods for configuring ChainSharp.Effect.Provider.Parameter services in the dependency injection container.
/// </summary>
/// <remarks>
/// The ServiceExtensions class contains utility methods that simplify the registration
/// of ChainSharp.Effect.Provider.Parameter services with the dependency injection system.
///
/// These extensions enable parameter serialization support for the ChainSharp.Effect system,
/// allowing workflow input and output parameters to be serialized to and from JSON format.
///
/// By using these extensions, applications can easily configure and use the
/// ChainSharp.Effect.Provider.Parameter system with minimal boilerplate code.
/// </remarks>
public static class ServiceExtensions
{
    /// <summary>
    /// Adds parameter serialization support to the ChainSharp effect configuration builder.
    /// </summary>
    /// <param name="builder">The ChainSharp effect configuration builder</param>
    /// <param name="jsonSerializerOptions">Optional JSON serializer options to use for parameter serialization</param>
    /// <returns>The configuration builder for method chaining</returns>
    /// <remarks>
    /// This method configures the ChainSharp.Effect system to serialize workflow input and output
    /// parameters to JSON format. It registers the necessary services with the dependency
    /// injection container and configures the JSON serialization options.
    ///
    /// The method performs the following steps:
    /// 1. Sets the JSON serializer options to use for parameter serialization
    /// 2. Registers the ParameterEffectProviderFactory as an IEffectProviderFactory
    ///
    /// If no JSON serializer options are provided, the default options from ChainSharpJsonSerializationOptions
    /// are used. These default options are configured to handle common serialization scenarios
    /// in the ChainSharp.Effect system.
    ///
    /// Example usage:
    /// ```csharp
    /// services.AddChainSharpEffects(options =>
    ///     options.SaveWorkflowParameters()
    /// );
    /// ```
    ///
    /// Or with custom JSON serializer options:
    /// ```csharp
    /// var jsonOptions = new JsonSerializerOptions
    /// {
    ///     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    ///     WriteIndented = true
    /// };
    ///
    /// services.AddChainSharpEffects(options =>
    ///     options.SaveWorkflowParameters(jsonOptions)
    /// );
    /// ```
    /// </remarks>
    public static ChainSharpEffectConfigurationBuilder SaveWorkflowParameters(
        this ChainSharpEffectConfigurationBuilder builder,
        JsonSerializerOptions? jsonSerializerOptions = null
    )
    {
        jsonSerializerOptions ??= ChainSharpJsonSerializationOptions.Default;

        builder.WorkflowParameterJsonSerializerOptions = jsonSerializerOptions;

        return builder.AddEffect<ParameterEffectProviderFactory>();
    }
}
