namespace ChainSharp.Effect.Data.Services.EffectLogger;

/// <summary>
/// Logger for Workflow injection.
/// </summary>
public class EffectLogger : IEffectLogger
{
    public void Info(string message)
    {
        Console.WriteLine($"INFO: {message}");
    }

    public void Debug(string message)
    {
        Console.WriteLine($"DEBUG: {message}");
    }

    public void Warning(string message)
    {
        Console.WriteLine($"WARNING: {message}");
    }

    public void Error(string message)
    {
        Console.WriteLine($"ERROR: {message}");
    }

    public void Error(string message, Exception exception)
    {
        Console.WriteLine($"ERROR: {message}");
    }
}
