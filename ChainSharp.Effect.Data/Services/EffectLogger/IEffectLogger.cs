namespace ChainSharp.Effect.Data.Services.EffectLogger;

/// <summary>
/// Logger for Workflow injection.
/// </summary>
public interface IEffectLogger
{
    public void Info(string message);

    public void Debug(string message);

    public void Warning(string message);

    public void Error(string message);

    public void Error(string message, Exception exception);
}
