using ChainSharp.Logging.Services.LoggingProviderContext;

namespace ChainSharp.Logging.Services.LoggingProviderContextFactory;

/// <summary>
/// Factory for each LoggingProviderContext.
/// Each provider will likely implement this on their own.
/// </summary>
public interface ILoggingProviderContextFactory
{
    public ILoggingProviderContext Create();
}
