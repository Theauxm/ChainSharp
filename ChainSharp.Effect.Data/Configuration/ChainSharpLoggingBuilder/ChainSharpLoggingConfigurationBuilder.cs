using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Data.Configuration.ChainSharpLoggingBuilder;

public class ChainSharpLoggingConfigurationBuilder(IServiceCollection serviceCollection)
{
    public IServiceCollection ServiceCollection => serviceCollection;

    protected internal ChainSharpLoggingConfiguration.ChainSharpLoggingConfiguration Build() =>
        new();
}
