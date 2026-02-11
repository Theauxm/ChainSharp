using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Extensions;

public static class ChainSharpEffectConfigurationBuilderExtensions
{
    public static ChainSharpEffectConfigurationBuilder SetEffectLogLevel(
        this ChainSharpEffectConfigurationBuilder builder,
        LogLevel logLevel
    )
    {
        builder.LogLevel = logLevel;
        return builder;
    }
}
