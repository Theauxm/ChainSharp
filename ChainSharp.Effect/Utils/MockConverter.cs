using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Utils;

public class MockConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.FullName != null && typeToConvert.FullName.StartsWith("Moq.Mock");
    }

    public override object Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        throw new NotImplementedException("Deserializing Moq objects is not supported");
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("MockType", value.GetType().Name);
        if (value.GetType().GenericTypeArguments.Length > 0)
            writer.WriteString("MockedType", value.GetType().GenericTypeArguments[0].Name);
        writer.WriteEndObject();
    }
}
