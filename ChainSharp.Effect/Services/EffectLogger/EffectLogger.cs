using LanguageExt;

namespace ChainSharp.Effect.Services.EffectLogger;

/// <summary>
/// Logger for Workflow injection.
/// </summary>
public class EffectLogger : IEffectLogger
{
    public Unit Info(string message)
    {
        Console.WriteLine($"INFO: {message}");

        return Unit.Default;
    }

    public Unit Debug(string message)
    {
        Console.WriteLine($"DEBUG: {message}");

        return Unit.Default;
    }

    public Unit Warning(string message)
    {
        Console.WriteLine($"WARNING: {message}");

        return Unit.Default;
    }

    public Unit Error(string message)
    {
        Console.WriteLine($"ERROR: {message}");

        return Unit.Default;
    }

    public Unit Error(string message, Exception exception)
    {
        Console.WriteLine($"ERROR: {message}");

        return Unit.Default;
    }
}
