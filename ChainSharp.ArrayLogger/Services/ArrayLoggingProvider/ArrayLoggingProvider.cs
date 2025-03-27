using Microsoft.Extensions.Logging;

namespace ChainSharp.ArrayLogger.Services.ArrayLoggingProvider;

public class ArrayLoggingProvider : IArrayLoggingProvider
{
    public List<ArrayLoggerEffect> Loggers { get; } = [];

    public void Dispose() { }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new ArrayLoggerEffect(categoryName);

        Loggers.Add(logger);

        return logger;
    }
}
