using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Utils;

public class SystemTypeConverter : JsonConverter<Type>
{
    public override Type? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var assemblyQualifiedName = reader.GetString();

        if (assemblyQualifiedName == null)
            return null;

        var resultType = Type.GetType(assemblyQualifiedName);

        if (resultType == null)
            throw new JsonException($"Unable to find type: {assemblyQualifiedName}");

        return resultType;
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.AssemblyQualifiedName);
    }
}
