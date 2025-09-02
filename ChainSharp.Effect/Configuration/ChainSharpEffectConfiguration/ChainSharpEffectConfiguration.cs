using System.Text.Json;
using ChainSharp.Effect.Utils;
using Newtonsoft.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;

public class ChainSharpEffectConfiguration : IChainSharpEffectConfiguration
{
    public JsonSerializerOptions SystemJsonJsonSerializerOptions { get; set; } =
        ChainSharpJsonSerializationOptions.Default;

    public JsonSerializerSettings NewtonsoftJsonSerializerSettings { get; } =
        ChainSharpJsonSerializationOptions.NewtonsoftDefault;

    public bool SerializeStepData { get; } = false;
}
