using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.ArrayLogger.Services.ArrayLoggingProvider;

public interface IArrayLoggingProvider : ILoggerProvider
{
    public List<ArrayLoggerEffect> Loggers { get; }
}
