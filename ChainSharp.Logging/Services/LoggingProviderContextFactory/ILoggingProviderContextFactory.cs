using ChainSharp.Logging.Services.LoggingProviderContext;

namespace ChainSharp.Logging.Services.LoggingProviderContextFactory;

public interface ILoggingProviderContextFactory
{
    public ILoggingProviderContext Create();
}
