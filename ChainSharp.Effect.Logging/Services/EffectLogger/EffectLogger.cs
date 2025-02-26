using ChainSharp.Effect.Services.EffectLogger;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Log.Services.EffectLogger;

public class EffectLogger(ILogger<EffectLogger> logger) : IEffectLogger
{
    public Unit Info(string message)
    {
        logger.LogInformation(message);

        return Unit.Default;
    }

    public Unit Debug(string message)
    {
        logger.LogDebug(message);

        return Unit.Default;
    }

    public Unit Warning(string message)
    {
        logger.LogWarning(message);

        return Unit.Default;
    }

    public Unit Error(string message)
    {
        logger.LogError(message);

        return Unit.Default;
    }

    public Unit Error(string message, Exception exception)
    {
        logger.LogError(message, exception);

        return Unit.Default;
    }

    public Unit Critical(string message)
    {
        logger.LogCritical(message);

        return Unit.Default;
    }

    public Unit Trace(string message)
    {
        logger.LogTrace(message);

        return Unit.Default;
    }

    public Unit Log(string message)
    {
        logger.Log(LogLevel.Information, message);

        return Unit.Default;
    }
}
