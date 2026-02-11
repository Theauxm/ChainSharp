using System.Text.Json;
using ChainSharp.Effect.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;

public class ChainSharpEffectConfiguration : IChainSharpEffectConfiguration
{
    public JsonSerializerOptions SystemJsonSerializerOptions { get; set; } =
        ChainSharpJsonSerializationOptions.Default;

    public JsonSerializerSettings NewtonsoftJsonSerializerSettings { get; set; } =
        ChainSharpJsonSerializationOptions.NewtonsoftDefault;

    public static JsonSerializerOptions StaticSystemJsonSerializerOptions { get; set; } =
        JsonSerializerOptions.Default;

    public bool SerializeStepData { get; set; } = false;

    public LogLevel LogLevel { get; set; } = LogLevel.Debug;
}
