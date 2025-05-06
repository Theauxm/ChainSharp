# ChainSharp.Effect.Json Architecture

ChainSharp.Effect.Json provides a lightweight effect for serializing and logging models as JSON during workflow execution. This document explains the architecture, key components, and usage patterns of this effect.

## Introduction

The JSON effect is a simple but powerful tool for debugging and monitoring ChainSharp.Effect workflows. It:

1. Tracks models created during workflow execution
2. Serializes them to JSON using System.Text.Json
3. Logs the serialized JSON to the configured logger
4. Only logs changes when models are updated

Unlike database effects that persist models to storage, the JSON effect is primarily for observability and debugging. It provides a way to see what's happening inside your workflows without the overhead of a database.

## Core Components

### JsonEffectProvider

The `JsonEffectProvider` is the main component that implements the `IEffectProvider` interface:

```csharp
public class JsonEffectProvider(
    ILogger<JsonEffectProvider> logger,
    IChainSharpEffectConfiguration configuration
) : IJsonEffectProvider
{
    private readonly Dictionary<IModel, string> _previousStates = new();
    private readonly HashSet<IModel> _trackedModels = [];

    public void Dispose() { }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        var options = configuration.WorkflowParameterJsonSerializerOptions;

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
    }

    public async Task Track(IModel model)
    {
        if (_trackedModels.Add(model))
        {
            // Store initial serialized state when tracking starts
            _previousStates[model] = JsonSerializer.Serialize(
                model,
                model.GetType(),
                configuration.WorkflowParameterJsonSerializerOptions
            );
        }
    }
}
```

Key features:
- Tracks models in a HashSet for efficient lookup
- Stores the serialized state of each model
- Detects changes by comparing serialized states
- Logs only changed models when SaveChanges is called
- Uses the configuration's JSON serialization options

### IJsonEffectProvider

The `IJsonEffectProvider` interface is a marker interface that extends `IEffectProvider`:

```csharp
public interface IJsonEffectProvider : IEffectProvider { }
```

This interface allows for dependency injection and potential future extensions.

### JsonEffectProviderFactory

The `JsonEffectProviderFactory` creates instances of the `JsonEffectProvider`:

```csharp
public class JsonEffectProviderFactory(IServiceProvider serviceProvider) : IEffectProviderFactory
{
    public IEffectProvider Create() => serviceProvider.GetRequiredService<IJsonEffectProvider>();
}
```

This factory:
- Resolves the `IJsonEffectProvider` from the service provider
- Returns it as an `IEffectProvider` for the EffectRunner

## How It Works

### Model Tracking

When an EffectWorkflow calls `EffectRunner.Track(model)`, the following happens:

1. The EffectRunner forwards the model to all registered effect providers
2. The JsonEffectProvider adds the model to its `_trackedModels` collection
3. The provider serializes the model to JSON and stores it in `_previousStates`

This initial tracking establishes a baseline state for the model.

### Change Detection

When an EffectWorkflow calls `EffectRunner.SaveChanges()`, the following happens:

1. The EffectRunner forwards the call to all registered effect providers
2. The JsonEffectProvider iterates through all tracked models
3. For each model, it serializes the current state to JSON
4. It compares this JSON with the previously stored JSON
5. If they differ, it updates the stored state and adds the model to a changed list
6. It logs the new JSON state for each changed model

This change detection ensures that only meaningful updates are logged, reducing noise.

### Serialization Process

The JsonEffectProvider uses System.Text.Json for serialization with options from the ChainSharpEffectConfiguration:

```csharp
var options = configuration.WorkflowParameterJsonSerializerOptions;
var json = JsonSerializer.Serialize(model, model.GetType(), options);
```

These options typically include:
- Indented formatting for readability
- Camel case property names
- Custom converters for specific types
- Handling of circular references

### Logging Mechanism

The JsonEffectProvider logs serialized models using the injected ILogger:

```csharp
logger.LogInformation(_previousStates[model]);
```

By default, it logs at the Information level, which means:
- The logs will be visible in most logging configurations
- They can be filtered out in production if needed
- They provide a clear record of model changes during workflow execution

## Integration with ChainSharp.Effect

The JSON effect integrates with ChainSharp.Effect by:

1. Implementing the `IEffectProvider` interface
2. Being registered with the EffectRunner through the effect system
3. Receiving models and SaveChanges calls from the EffectRunner

This integration allows it to work alongside other effects, such as database effects. For example, you can use both the JSON effect and a PostgreSQL effect to log models and persist them to a database simultaneously.

## Service Registration

The JSON effect is registered with the dependency injection container using the `AddJsonEffect` extension method:

```csharp
public static ChainSharpEffectConfigurationBuilder AddJsonEffect(
    this ChainSharpEffectConfigurationBuilder configurationBuilder
)
{
    configurationBuilder.ServiceCollection.AddTransient<
        IJsonEffectProvider,
        JsonEffectProvider
    >();

    return configurationBuilder.AddEffect<IEffectProviderFactory, JsonEffectProviderFactory>();
}
```

This method:
1. Registers the `JsonEffectProvider` as a transient service
2. Registers the `JsonEffectProviderFactory` with the effect system

## Usage Examples

### Basic Usage

To use the JSON effect in your application:

```csharp
// In Program.cs or Startup.cs
services.AddChainSharpEffects(options => 
    options.AddJsonEffect()
);
```

This will register the JSON effect with the ChainSharp.Effect system. When workflows are executed, their models will be serialized and logged.

### Combining with Other Effects

The JSON effect can be combined with other effects:

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddJsonEffect()
        .AddPostgresEffect(connectionString)
);
```

This configuration will:
1. Log models as JSON for debugging
2. Persist models to PostgreSQL for storage

### Configuring Logging

To control the verbosity of JSON logging, configure the logger:

```csharp
services.AddLogging(builder => 
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information)
);
```

You can also filter logs by category:

```csharp
services.AddLogging(builder => 
    builder
        .AddFilter<ConsoleLoggerProvider>("ChainSharp.Effect.Json", LogLevel.Debug)
        .AddFilter<ConsoleLoggerProvider>("*", LogLevel.Warning)
);
```

This will show detailed logs for the JSON effect while limiting other logs to warnings and errors.

## Best Practices

### When to Use the JSON Effect

The JSON effect is most useful for:

1. **Development**: See what's happening inside workflows during development
2. **Debugging**: Diagnose issues by examining model states
3. **Testing**: Verify that models are being created and updated correctly
4. **Monitoring**: Keep an eye on workflow execution in non-production environments

It's less suitable for:
1. **Production**: Can generate large volumes of logs
2. **Persistence**: Does not store data beyond the log retention period
3. **Performance-critical paths**: Adds serialization overhead

### Logging Configuration

For optimal use of the JSON effect:

1. **Set appropriate log levels**:
   - Development: Information or Debug
   - Testing: Information
   - Production: Warning or Error (if used at all)

2. **Configure structured logging**:
   - Use a logging provider that supports structured data (like Serilog)
   - This allows better querying and filtering of the JSON data

3. **Consider log storage**:
   - Ensure logs are stored somewhere accessible for analysis
   - Consider using a log aggregation service for larger applications

### Performance Considerations

The JSON effect adds some overhead due to serialization:

1. **Large models**: Be cautious with very large models, as serialization can be expensive
2. **High-frequency updates**: Consider the impact on performance if models change frequently
3. **Memory usage**: The effect stores serialized states in memory, which can add up

To mitigate these concerns:
- Use the JSON effect selectively in performance-critical workflows
- Consider custom JSON serialization options to limit the depth or properties serialized

### Security Considerations

Be mindful of sensitive data:

1. **PII and secrets**: The JSON effect will log whatever is in your models, including potentially sensitive data
2. **Log access**: Ensure logs containing serialized models are appropriately secured
3. **Custom serialization**: Consider using custom converters to redact sensitive fields

## Conclusion

ChainSharp.Effect.Json provides a simple but effective way to gain visibility into your workflows through JSON serialization and logging. While not intended for data persistence, it's an invaluable tool for development, debugging, and monitoring.

By implementing the IEffectProvider interface, it integrates seamlessly with the ChainSharp.Effect architecture and can work alongside other effects to provide a comprehensive solution for workflow execution and monitoring.
