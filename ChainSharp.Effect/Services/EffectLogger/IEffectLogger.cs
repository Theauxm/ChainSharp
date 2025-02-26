using LanguageExt;

namespace ChainSharp.Effect.Services.EffectLogger;

/// <summary>
/// Logger for Workflow injection.
/// </summary>
public interface IEffectLogger
{
    public Unit Info(string message);

    public Unit Debug(string message);

    public Unit Warning(string message);

    public Unit Error(string message);

    public Unit Error(string message, Exception exception);
}
