using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Services.Effect;
using ChainSharp.Effect.Services.EffectLogger;

namespace ChainSharp.Effect.Json.Services.JsonEffect;

public class JsonEffect(IEnumerable<IEffectLogger> loggers) : IEffect
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
            loggers.RunAll(logger => logger.Log(_previousStates[model]));
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
