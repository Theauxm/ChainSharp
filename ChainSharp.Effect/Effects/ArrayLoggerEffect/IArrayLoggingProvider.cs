using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Effects.ArrayLoggerEffect;

public interface IArrayLoggingProvider : ILoggerProvider
{
    public List<ArrayLoggerEffect> Loggers { get; }
}
