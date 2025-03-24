using System.Text.Json;
using ChainSharp.Effect.Utils;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;

public class ChainSharpEffectConfiguration : IChainSharpEffectConfiguration
{
    public JsonSerializerOptions WorkflowParameterJsonSerializerOptions { get; set; } =
        ChainSharpJsonSerializationOptions.Default;
}
