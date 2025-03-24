using System.Text.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;

public class ChainSharpEffectConfiguration : IChainSharpEffectConfiguration
{
    public JsonSerializerOptions WorkflowParameterJsonSerializerOptions { get; set; } =
        JsonSerializerOptions.Default;
}
