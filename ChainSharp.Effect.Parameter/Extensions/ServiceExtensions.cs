using System.Text.Json;
using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Parameter.Services.ParameterEffectProviderFactory;
using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Utils;

namespace ChainSharp.Effect.Parameter.Extensions;

public static class ServiceExtensions
{
    public static ChainSharpEffectConfigurationBuilder SaveWorkflowParameters(
        this ChainSharpEffectConfigurationBuilder builder,
        JsonSerializerOptions? jsonSerializerOptions = null
    )
    {
        jsonSerializerOptions ??= ChainSharpJsonSerializationOptions.Default;

        builder.WorkflowParameterJsonSerializerOptions = jsonSerializerOptions;

        return builder.AddEffect<IEffectProviderFactory, ParameterEffectProviderFactory>();
    }
}
