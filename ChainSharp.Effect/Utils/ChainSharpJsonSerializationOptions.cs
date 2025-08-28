using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Utils;

/// <summary>
/// Provides standardized JSON serialization options for the ChainSharp.Effect system.
/// </summary>
/// <remarks>
/// The ChainSharpJsonSerializationOptions class defines a set of default serialization
/// options that are used throughout the ChainSharp.Effect system. These options ensure
/// consistent serialization behavior across different components.
///
/// The default options include:
/// 1. Indented JSON output for better readability
/// 2. Inclusion of fields (not just properties) in serialization
/// 3. Omission of null values to reduce output size
/// 4. Custom converters for special types:
///    - Enums as strings for better readability and interoperability
///    - ValueTuples for proper serialization of tuple types
///    - Mock objects for handling Moq mocks in test scenarios
///    - System.Type objects for type information persistence
///
/// These options can be used by any component that needs to serialize or deserialize
/// JSON data in the ChainSharp.Effect system, ensuring consistent behavior.
/// </remarks>
public static class ChainSharpJsonSerializationOptions
{
    /// <summary>
    /// Gets or sets the default JSON serializer options for the ChainSharp.Effect system.
    /// </summary>
    /// <remarks>
    /// These options are used by default throughout the system for JSON serialization
    /// and deserialization. They provide a consistent configuration that handles the
    /// special types and formatting requirements of the ChainSharp.Effect system.
    ///
    /// The options include:
    /// - WriteIndented: true - Produces formatted, indented JSON for better readability
    /// - IncludeFields: true - Includes fields (not just properties) in serialization
    /// - DefaultIgnoreCondition: WhenWritingNull - Omits null values from the output
    /// - Converters:
    ///   - JsonStringEnumConverter - Serializes enums as strings instead of numbers
    ///   - ValueTupleConverter - Handles serialization of ValueTuple types
    ///   - MockConverter - Handles serialization of Moq mock objects
    ///   - SystemTypeConverter - Handles serialization of System.Type objects
    ///
    /// These options can be modified if needed, but changing them may affect the
    /// behavior of components that rely on the default configuration.
    /// </remarks>
    public static JsonSerializerOptions Default { get; set; } =
        new()
        {
            WriteIndented = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.Preserve,
            MaxDepth = 8,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ValueTupleConverter(),
                new MockConverter(),
                new SystemTypeConverter(),
            }
        };
}
