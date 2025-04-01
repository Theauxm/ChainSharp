using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Utils;

public static class ChainSharpJsonSerializationOptions
{
    public static JsonSerializerOptions Default { get; set; } =
        new()
        {
            WriteIndented = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ValueTupleConverter(),
                new MockConverter()
            }
        };
}
