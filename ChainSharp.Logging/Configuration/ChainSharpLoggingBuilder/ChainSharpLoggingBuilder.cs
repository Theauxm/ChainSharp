using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Logging.Configuration.ChainSharpLoggingBuilder;

public class ChainSharpLoggingBuilder(IServiceCollection serviceCollection)
{
    public IServiceCollection ServiceCollection => serviceCollection;

    protected internal ChainSharpLoggingConfiguration.ChainSharpLoggingConfiguration Build() =>
        new();
}
