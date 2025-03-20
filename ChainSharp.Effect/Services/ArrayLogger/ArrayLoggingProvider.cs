using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Services.ArrayLogger;

public class ArrayLoggingProvider : IArrayLoggingProvider
{
    public List<ArrayLogger> Loggers { get; } = [];

    public void Dispose() { }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new ArrayLogger(categoryName);

        Loggers.Add(logger);

        return logger;
    }
}
