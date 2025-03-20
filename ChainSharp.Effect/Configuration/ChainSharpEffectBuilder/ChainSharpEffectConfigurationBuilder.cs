using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;

public class ChainSharpEffectConfigurationBuilder(IServiceCollection serviceCollection)
{
    public IServiceCollection ServiceCollection => serviceCollection;

    public bool PostgresEffectsEnabled { get; set; } = false;

    protected internal ChainSharpEffectConfiguration.ChainSharpEffectConfiguration Build() => new();
}
