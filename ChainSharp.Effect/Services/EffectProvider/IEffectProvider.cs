using ChainSharp.Effect.Models;

namespace ChainSharp.Effect.Services.EffectProvider;

/// <summary>
/// Defines the contract for a component that tracks and persists workflow metadata.
/// </summary>
/// <remarks>
/// The IEffectProvider interface is a key abstraction in the ChainSharp.Effect system.
/// It represents a mechanism for tracking and persisting workflow metadata, such as
/// execution details, inputs, outputs, and error information.
/// 
/// Different implementations of this interface can store metadata in different ways,
/// such as in a database, in memory, or in a file. This allows for flexibility in
/// how workflow execution is tracked and monitored.
/// 
/// The provider pattern used here allows for:
/// 1. Separation of concerns between workflow execution and metadata tracking
/// 2. Multiple tracking mechanisms to be used simultaneously
/// 3. Easy extension with new tracking mechanisms
/// </remarks>
public interface IEffectProvider : IDisposable
{
    /// <summary>
    /// Persists any pending changes to the underlying storage mechanism.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method should ensure that all tracked models are persisted to the
    /// provider's storage mechanism. The actual implementation depends on the
    /// specific provider, but typically involves writing to a database, file,
    /// or other persistent storage.
    /// </remarks>
    Task SaveChanges(CancellationToken cancellationToken);

    /// <summary>
    /// Registers a model to be tracked by this provider.
    /// </summary>
    /// <param name="model">The model to track</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method should prepare the model for persistence, but not necessarily
    /// persist it immediately. The actual persistence typically occurs when
    /// SaveChanges is called.
    /// 
    /// Models typically represent workflow metadata, such as execution details,
    /// inputs, outputs, and error information.
    /// </remarks>
    Task Track(IModel model);
}
