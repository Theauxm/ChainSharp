namespace ChainSharp.Effect.Services.EffectRegistry;

/// <summary>
/// Provides runtime toggling of observational effect providers.
/// Effects not tracked by the registry are considered always-enabled (infrastructure effects).
/// </summary>
public interface IEffectRegistry
{
    /// <summary>
    /// Returns whether the given factory type is enabled.
    /// Returns true if the type is not tracked by the registry (infrastructure effects).
    /// </summary>
    bool IsEnabled(Type factoryType);

    /// <summary>
    /// Returns whether the given factory type is enabled.
    /// Returns true if the type is not tracked by the registry (infrastructure effects).
    /// </summary>
    bool IsEnabled<TFactory>();

    /// <summary>
    /// Enables the effect associated with the given factory type.
    /// No-op if the type is not tracked by the registry.
    /// </summary>
    void Enable(Type factoryType);

    /// <summary>
    /// Enables the effect associated with the given factory type.
    /// No-op if the type is not tracked by the registry.
    /// </summary>
    void Enable<TFactory>();

    /// <summary>
    /// Disables the effect associated with the given factory type.
    /// No-op if the type is not tracked by the registry.
    /// </summary>
    void Disable(Type factoryType);

    /// <summary>
    /// Disables the effect associated with the given factory type.
    /// No-op if the type is not tracked by the registry.
    /// </summary>
    void Disable<TFactory>();

    /// <summary>
    /// Returns all tracked factory types and their enabled/disabled status.
    /// </summary>
    IReadOnlyDictionary<Type, bool> GetAll();

    /// <summary>
    /// Registers a factory type as toggleable in the registry.
    /// Called internally during service configuration.
    /// </summary>
    void Register(Type factoryType, bool enabled = true);
}
