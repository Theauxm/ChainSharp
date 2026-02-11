using System.Text.Json;
using ChainSharp.Effect.Services.EffectRegistry;
using ChainSharp.Effect.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;

public class ChainSharpEffectConfigurationBuilder(
    IServiceCollection serviceCollection,
    IEffectRegistry? effectRegistry = null
)
{
    public IServiceCollection ServiceCollection => serviceCollection;

    public IEffectRegistry? EffectRegistry => effectRegistry;

    public bool DataContextLoggingEffectEnabled { get; set; } = false;

    public bool SerializeStepData { get; set; } = false;

    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    public JsonSerializerOptions WorkflowParameterJsonSerializerOptions { get; set; } =
        ChainSharpJsonSerializationOptions.Default;

    public JsonSerializerSettings NewtonsoftJsonSerializerSettings { get; set; } =
        ChainSharpJsonSerializationOptions.NewtonsoftDefault;

    protected internal ChainSharpEffectConfiguration.ChainSharpEffectConfiguration Build()
    {
        var configuration = new ChainSharpEffectConfiguration.ChainSharpEffectConfiguration
        {
            SystemJsonSerializerOptions = WorkflowParameterJsonSerializerOptions,
            NewtonsoftJsonSerializerSettings = NewtonsoftJsonSerializerSettings,
            SerializeStepData = SerializeStepData,
            LogLevel = LogLevel,
        };

        ChainSharpEffectConfiguration
            .ChainSharpEffectConfiguration
            .StaticSystemJsonSerializerOptions = WorkflowParameterJsonSerializerOptions;

        return configuration;
    }
}
