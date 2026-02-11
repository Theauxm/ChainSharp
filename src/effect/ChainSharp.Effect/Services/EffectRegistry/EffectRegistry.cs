using System.Collections.Concurrent;

namespace ChainSharp.Effect.Services.EffectRegistry;

/// <summary>
/// Thread-safe implementation of IEffectRegistry using ConcurrentDictionary.
/// Registered as singleton in the DI container.
/// </summary>
public class EffectRegistry : IEffectRegistry
{
    private readonly ConcurrentDictionary<Type, bool> _effects = new();

    public bool IsEnabled(Type factoryType)
    {
        // Untracked types are always enabled (infrastructure effects)
        return !_effects.TryGetValue(factoryType, out var enabled) || enabled;
    }

    public bool IsEnabled<TFactory>() => IsEnabled(typeof(TFactory));

    public void Enable(Type factoryType)
    {
        _effects.AddOrUpdate(factoryType, true, (_, _) => true);
    }

    public void Enable<TFactory>() => Enable(typeof(TFactory));

    public void Disable(Type factoryType)
    {
        _effects.AddOrUpdate(factoryType, false, (_, _) => false);
    }

    public void Disable<TFactory>() => Disable(typeof(TFactory));

    public IReadOnlyDictionary<Type, bool> GetAll()
    {
        return new Dictionary<Type, bool>(_effects);
    }

    public void Register(Type factoryType, bool enabled = true)
    {
        _effects.TryAdd(factoryType, enabled);
    }
}
