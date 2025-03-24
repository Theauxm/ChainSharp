using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Utils;

public class ValueTupleConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is { IsValueType: true, IsGenericType: true }
        && typeToConvert.FullName!.StartsWith("System.ValueTuple");

    public override JsonConverter? CreateConverter(Type type, JsonSerializerOptions options)
    {
        Type converterType = typeof(ValueTupleConverterInner<>).MakeGenericType(type);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }

    private class ValueTupleConverterInner<T> : JsonConverter<T>
        where T : struct
    {
        private static readonly FieldInfo[] Fields = typeof(T).GetFields();

        public override T Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            var values =
                JsonSerializer.Deserialize<object[]>(ref reader, options)
                ?? throw new JsonException("Failed to deserialize ValueTuple");

            if (values.Length != Fields.Length)
                throw new JsonException(
                    $"Expected {Fields.Length} values, but got {values.Length}"
                );

            object?[] args = values
                .Zip(Fields, (val, field) => Convert.ChangeType(val, field.FieldType))
                .ToArray();
            return (T)Activator.CreateInstance(typeof(T), args)!;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var values = Fields.Select(f => f.GetValue(value)!).ToArray();
            JsonSerializer.Serialize(writer, values, options);
        }
    }
}
