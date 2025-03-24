using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;

public class ChainSharpEffectConfigurationBuilder(IServiceCollection serviceCollection)
{
    public IServiceCollection ServiceCollection => serviceCollection;

    public bool PostgresEffectsEnabled { get; set; } = false;

    public JsonSerializerOptions WorkflowParameterJsonSerializerOptions { get; set; } =
        ChainSharpJsonSerializationOptions.Default;

    protected internal ChainSharpEffectConfiguration.ChainSharpEffectConfiguration Build() =>
        new() { WorkflowParameterJsonSerializerOptions = WorkflowParameterJsonSerializerOptions };
}
