using System.Text.Json;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectProvider;

namespace ChainSharp.Effect.Effects.ParameterEffect;

public class ParameterEffect(JsonSerializerOptions options) : IEffectProvider
{
    private readonly HashSet<Metadata> _trackedMetadatas = [];

    public void Dispose() { }

    public async Task SaveChanges()
    {
        foreach (var metadata in _trackedMetadatas)
        {
            SerializeParameters(metadata);
        }
    }

    public async Task Track(IModel model)
    {
        if (model is Metadata metadata)
        {
            _trackedMetadatas.Add(metadata);
            SerializeParameters(metadata);
        }
    }

    private void SerializeParameters(Metadata metadata)
    {
        if (metadata.InputObject is not null)
        {
            var serializedInput = JsonSerializer.Serialize(metadata.InputObject, options);
            metadata.Input = JsonDocument.Parse(serializedInput);
        }

        if (metadata.OutputObject is not null)
        {
            var serializedOutput = JsonSerializer.Serialize(metadata.OutputObject, options);
            metadata.Output = JsonDocument.Parse(serializedOutput);
        } 
    }
}