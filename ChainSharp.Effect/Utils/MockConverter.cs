using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Utils;

/// <summary>
/// Provides JSON serialization support for Moq mock objects.
/// </summary>
/// <remarks>
/// The MockConverter is a custom JSON converter that enables Moq.Mock objects
/// to be properly serialized to JSON. This is necessary because mock objects
/// from the Moq library cannot be directly serialized by System.Text.Json.
/// 
/// This converter handles serialization of mock objects by creating a simplified
/// representation that includes the mock type and the type being mocked. It does
/// not support deserialization, as recreating mock objects from JSON is not
/// a typical use case and would be complex to implement correctly.
/// 
/// This converter is particularly useful in the ChainSharp.Effect system because:
/// 1. Mocks are often used in testing scenarios
/// 2. These mocks might be part of workflow inputs or outputs that need to be serialized
/// 3. Without this converter, serialization would fail when mock objects are encountered
/// 
/// The converter is registered in the ChainSharpJsonSerializationOptions.Default
/// options, making it available throughout the system.
/// </remarks>
public class MockConverter : JsonConverter<object>
{
    /// <summary>
    /// Determines whether the converter can convert the specified type.
    /// </summary>
    /// <param name="typeToConvert">The type to check</param>
    /// <returns>True if the type is a Moq.Mock; otherwise, false</returns>
    /// <remarks>
    /// This method checks if the type's full name starts with "Moq.Mock",
    /// which identifies all mock objects created by the Moq library.
    /// 
    /// This includes both generic mocks (Mock&lt;T&gt;) and non-generic mocks,
    /// though the latter are less common in practice.
    /// </remarks>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.FullName != null && typeToConvert.FullName.StartsWith("Moq.Mock");
    }

    /// <summary>
    /// Reads a JSON value and converts it to a mock object.
    /// </summary>
    /// <param name="reader">The JSON reader</param>
    /// <param name="typeToConvert">The type to convert to</param>
    /// <param name="options">The serializer options</param>
    /// <returns>This method does not return as it throws an exception</returns>
    /// <exception cref="NotImplementedException">Always thrown as deserialization is not supported</exception>
    /// <remarks>
    /// Deserialization of mock objects is not supported because:
    /// 1. It would require recreating the mock with all its setup and behavior
    /// 2. This information is not typically serialized in the first place
    /// 3. There's rarely a need to deserialize mock objects in practice
    /// 
    /// If deserialization is attempted, this method will throw a NotImplementedException.
    /// </remarks>
    public override object Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        throw new NotImplementedException("Deserializing Moq objects is not supported");
    }

    /// <summary>
    /// Writes a mock object as JSON.
    /// </summary>
    /// <param name="writer">The JSON writer</param>
    /// <param name="value">The mock object to write</param>
    /// <param name="options">The serializer options</param>
    /// <remarks>
    /// This method serializes a mock object as a simple JSON object with:
    /// 1. A "MockType" property containing the name of the mock class
    /// 2. A "MockedType" property containing the name of the mocked interface or class
    ///    (only included if the mock is generic)
    /// 
    /// For example, a Mock&lt;ILogger&gt; would be serialized as:
    /// {
    ///   "MockType": "Mock`1",
    ///   "MockedType": "ILogger"
    /// }
    /// 
    /// This simplified representation allows for identification of the mock
    /// in serialized data without attempting to capture its full state.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("MockType", value.GetType().Name);
        if (value.GetType().GenericTypeArguments.Length > 0)
            writer.WriteString("MockedType", value.GetType().GenericTypeArguments[0].Name);
        writer.WriteEndObject();
    }
}
