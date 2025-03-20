using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Services.ArrayLogger;

public interface IArrayLoggingProvider : ILoggerProvider
{
    public List<ArrayLogger> Loggers { get; }
}
