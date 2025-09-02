using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;

public class ChainSharpEffectConfigurationBuilder(IServiceCollection serviceCollection)
{
    public IServiceCollection ServiceCollection => serviceCollection;

    public bool DataContextLoggingEffectEnabled { get; set; } = false;

    public bool SerializeStepData { get; set; } = false;

    public JsonSerializerOptions WorkflowParameterJsonSerializerOptions { get; set; } =
        ChainSharpJsonSerializationOptions.Default;

    public JsonSerializerSettings NewtonsoftJsonSerializerSettings { get; set; } =
        ChainSharpJsonSerializationOptions.NewtonsoftDefault;

    protected internal ChainSharpEffectConfiguration.ChainSharpEffectConfiguration Build() =>
        new()
        {
            SystemJsonJsonSerializerOptions = WorkflowParameterJsonSerializerOptions,
            NewtonsoftJsonSerializerSettings = NewtonsoftJsonSerializerSettings,
            SerializeStepData = SerializeStepData
        };
}
