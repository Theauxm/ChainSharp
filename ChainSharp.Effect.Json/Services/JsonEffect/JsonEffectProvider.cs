using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Models;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Json.Services.JsonEffect;

public class JsonEffectProvider(ILogger<JsonEffectProvider> logger) : IJsonEffectProvider
{
    private readonly Dictionary<IModel, string> _previousStates = new();
    private readonly HashSet<IModel> _trackedModels = new();

    public void Dispose() { }

    public async Task SaveChanges()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() }
        };

        var changedModels = new List<IModel>();

        foreach (var model in _trackedModels)
        {
            var currentState = JsonSerializer.Serialize(model, model.GetType(), options);

            if (
                !_previousStates.TryGetValue(model, out var previousState)
                || previousState != currentState
            )
            {
                _previousStates[model] = currentState;
                changedModels.Add(model);
            }
        }

        foreach (var model in changedModels)
            logger.LogInformation(_previousStates[model]);
        // Console.WriteLine(_previousStates[model]);
    }

    public async Task Track(IModel model)
    {
        if (_trackedModels.Add(model))
        {
            // Store initial serialized state when tracking starts
            _previousStates[model] = JsonSerializer.Serialize(
                model,
                model.GetType(),
                new JsonSerializerOptions
                {
                    IncludeFields = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                }
            );
        }
    }
}
