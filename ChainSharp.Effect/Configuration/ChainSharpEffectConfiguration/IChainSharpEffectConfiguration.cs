using System.Text.Json;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;

public interface IChainSharpEffectConfiguration
{
    public JsonSerializerOptions WorkflowParameterJsonSerializerOptions { get; }
}
