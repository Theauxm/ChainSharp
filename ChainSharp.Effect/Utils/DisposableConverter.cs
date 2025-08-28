namespace ChainSharp.Effect.Utils;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class DisposableConverter : JsonConverter<object>
{
    // Prevent infinite recursion / cycles by tracking instances currently being written.
    private static readonly ThreadLocal<HashSet<object>> InProgress =
        new(() => new HashSet<object>(ReferenceEqualityComparer.Instance));

    public override bool CanConvert(Type typeToConvert) =>
        typeof(IDisposable).IsAssignableFrom(typeToConvert);

    public override object Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStringValue("[ IDisposable ]");
    }
}
