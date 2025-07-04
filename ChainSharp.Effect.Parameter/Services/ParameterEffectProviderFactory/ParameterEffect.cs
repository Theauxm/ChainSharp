using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectProvider;

namespace ChainSharp.Effect.Parameter.Services.ParameterEffectProviderFactory;

/// <summary>
/// Implements an effect provider that serializes workflow input and output parameters to JSON format.
/// </summary>
/// <remarks>
/// The ParameterEffect class provides an implementation of the IEffectProvider interface
/// that serializes workflow input and output parameters to JSON format.
///
/// This provider tracks metadata objects and serializes their input and output parameters
/// to JSON format when changes are saved. The serialized parameters are stored in the
/// metadata object's Input and Output properties, which can then be persisted to a database
/// or other storage medium.
///
/// This implementation is useful for capturing and storing the input and output parameters
/// of workflow executions, which can be used for auditing, debugging, and analytics purposes.
/// </remarks>
/// <param name="options">The JSON serializer options to use for parameter serialization</param>
public class ParameterEffect(JsonSerializerOptions options) : IEffectProvider
{
    private readonly HashSet<Metadata> _trackedMetadatas = [];
    private readonly List<JsonDocument> _jsonDocumentsToDispose = [];
    private readonly object _lock = new();
    private bool _disposed = false;

    /// <summary>
    /// Disposes the effect provider and releases any resources.
    /// </summary>
    /// <remarks>
    /// This implementation clears all tracked metadata objects and properly disposes
    /// any JsonDocument instances to prevent memory leaks.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            // Dispose existing JsonDocument instances to prevent memory leaks
            foreach (var metadata in _trackedMetadatas)
            {
                DisposeJsonDocuments(metadata);
            }

            // Dispose any queued JsonDocuments
            DisposeQueuedJsonDocuments();

            // Clear tracked metadata to release references and prevent memory leaks
            _trackedMetadatas.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// Saves changes to tracked metadata objects by serializing their input and output parameters.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method iterates through all tracked metadata objects and serializes their
    /// input and output parameters to JSON format. The serialized parameters are stored
    /// in the metadata object's Input and Output properties, which can then be persisted
    /// to a database or other storage medium.
    ///
    /// This allows for capturing and storing the input and output parameters of workflow
    /// executions, which can be used for auditing, debugging, and analytics purposes.
    /// </remarks>
    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            foreach (var metadata in _trackedMetadatas)
            {
                SerializeParameters(metadata);
            }

            // After serialization is complete, dispose queued JsonDocuments
            // This is safe because database persistence should happen after this method returns
            DisposeQueuedJsonDocuments();
        }
    }

    /// <summary>
    /// Begins tracking a model for changes.
    /// </summary>
    /// <param name="model">The model to track</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method checks if the specified model is a Metadata object, and if so,
    /// adds it to the set of tracked metadata objects and serializes its input and
    /// output parameters.
    ///
    /// Only Metadata objects are tracked by this provider, as they are the only objects
    /// that contain input and output parameters that need to be serialized.
    ///
    /// When a metadata object is first tracked, its input and output parameters are
    /// immediately serialized to JSON format. This ensures that the parameters are
    /// captured even if the SaveChanges method is never called.
    /// </remarks>
    public async Task Track(IModel model)
    {
        if (_disposed)
            return;

        if (model is Metadata metadata)
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _trackedMetadatas.Add(metadata);
                SerializeParameters(metadata);
            }
        }
    }

    /// <summary>
    /// Disposes JsonDocument instances in a metadata object to prevent memory leaks.
    /// </summary>
    /// <param name="metadata">The metadata object whose JsonDocuments to dispose</param>
    private static void DisposeJsonDocuments(Metadata metadata)
    {
        metadata.Input?.Dispose();
        metadata.Output?.Dispose();
    }

    /// <summary>
    /// Disposes all JsonDocument instances that are queued for disposal.
    /// </summary>
    /// <remarks>
    /// This method is called after serialization is complete to safely dispose
    /// old JsonDocument instances without interfering with database operations.
    /// </remarks>
    private void DisposeQueuedJsonDocuments()
    {
        foreach (var jsonDocument in _jsonDocumentsToDispose)
        {
            try
            {
                jsonDocument?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // JsonDocument was already disposed, safe to ignore
            }
        }
        _jsonDocumentsToDispose.Clear();
    }

    /// <summary>
    /// Serializes the input and output parameters of a metadata object to JSON format.
    /// </summary>
    /// <param name="metadata">The metadata object whose parameters to serialize</param>
    /// <remarks>
    /// This method serializes the input and output parameters of the specified metadata
    /// object to JSON format. The serialized parameters are stored in the metadata object's
    /// Input and Output properties, which can then be persisted to a database or other
    /// storage medium.
    ///
    /// If the input or output parameter is null, it is not serialized. This prevents
    /// overwriting existing serialized parameters with null values.
    ///
    /// The serialization is performed using the JSON serializer options provided to the
    /// constructor, which allows for customizing the serialization process.
    ///
    /// IMPORTANT: This method queues existing JsonDocument instances for disposal after
    /// database operations complete, preventing memory leaks while avoiding disposed object issues.
    /// </remarks>
    private void SerializeParameters(Metadata metadata)
    {
        if (metadata.InputObject is not null)
        {
            try
            {
                // Queue existing JsonDocument for disposal if it exists
                if (metadata.Input != null)
                {
                    _jsonDocumentsToDispose.Add(metadata.Input);
                }

                var serializedInput = JsonSerializer.Serialize(metadata.InputObject, options);
                metadata.Input = JsonDocument.Parse(serializedInput);
            }
            catch (ObjectDisposedException)
            {
                // Input object contains disposed JsonDocument, skip serialization
                // This can happen when metadata contains disposed JsonDocument objects
                if (metadata.Input == null)
                {
                    // Create a placeholder JsonDocument to indicate disposal occurred
                    var placeholderJson =
                        """{"_disposed": true, "_message": "Input object contained disposed JsonDocument objects"}""";
                    metadata.Input = JsonDocument.Parse(placeholderJson);
                }
            }
        }

        if (metadata.OutputObject is not null)
        {
            try
            {
                // Queue existing JsonDocument for disposal if it exists
                if (metadata.Output != null)
                {
                    _jsonDocumentsToDispose.Add(metadata.Output);
                }

                var serializedOutput = JsonSerializer.Serialize(metadata.OutputObject, options);
                metadata.Output = JsonDocument.Parse(serializedOutput);
            }
            catch (ObjectDisposedException)
            {
                // Output object contains disposed JsonDocument, skip serialization
                // This can happen when metadata contains disposed JsonDocument objects
                if (metadata.Output == null)
                {
                    // Create a placeholder JsonDocument to indicate disposal occurred
                    var placeholderJson =
                        """{"_disposed": true, "_message": "Output object contained disposed JsonDocument objects"}""";
                    metadata.Output = JsonDocument.Parse(placeholderJson);
                }
            }
        }
    }
}
