---
layout: default
title: Configuration
parent: API Reference
nav_order: 2
has_children: true
---

# Configuration

All ChainSharp Effect services are configured through a single entry point: `AddChainSharpEffects`. This method accepts a lambda that receives a `ChainSharpEffectConfigurationBuilder`, which provides a fluent API for adding data providers, effect providers, and orchestration services.

## Entry Point

```csharp
services.AddChainSharpEffects(options => options
    .AddPostgresEffect(connectionString)
    .AddEffectDataContextLogging()
    .AddJsonEffect()
    .SaveWorkflowParameters()
    .AddStepLogger()
    .AddEffectWorkflowBus(assemblies: typeof(Program).Assembly)
    .AddScheduler(scheduler => scheduler
        .UseHangfire(connectionString)
        .MaxActiveJobs(10)
    )
);
```

## Signature

```csharp
public static IServiceCollection AddChainSharpEffects(
    this IServiceCollection serviceCollection,
    Action<ChainSharpEffectConfigurationBuilder>? options = null
)
```

## Builder Properties

These properties can be set directly on the `ChainSharpEffectConfigurationBuilder`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceCollection` | `IServiceCollection` | (from constructor) | Direct access to the DI container for manual registrations |
| `SerializeStepData` | `bool` | `false` | Whether step input/output data should be serialized globally |
| `LogLevel` | `LogLevel` | `LogLevel.Debug` | Minimum log level for effect logging |
| `WorkflowParameterJsonSerializerOptions` | `JsonSerializerOptions` | `ChainSharpJsonSerializationOptions.Default` | System.Text.Json options for parameter serialization |
| `NewtonsoftJsonSerializerSettings` | `JsonSerializerSettings` | `ChainSharpJsonSerializationOptions.NewtonsoftDefault` | Newtonsoft.Json settings for legacy serialization |

## Extension Methods

| Method | Description |
|--------|-------------|
| [AddPostgresEffect]({{ site.baseurl }}{% link api-reference/configuration/add-postgres-effect.md %}) | Adds PostgreSQL database support for metadata persistence |
| [AddInMemoryEffect]({{ site.baseurl }}{% link api-reference/configuration/add-in-memory-effect.md %}) | Adds in-memory database support for testing/development |
| [AddEffectDataContextLogging]({{ site.baseurl }}{% link api-reference/configuration/add-effect-data-context-logging.md %}) | Enables logging for database operations |
| [AddJsonEffect]({{ site.baseurl }}{% link api-reference/configuration/add-json-effect.md %}) | Adds JSON change detection for tracking model mutations |
| [SaveWorkflowParameters]({{ site.baseurl }}{% link api-reference/configuration/save-workflow-parameters.md %}) | Serializes workflow input/output to JSON for persistence |
| [AddStepLogger]({{ site.baseurl }}{% link api-reference/configuration/add-step-logger.md %}) | Adds per-step execution logging |
| [AddEffectWorkflowBus]({{ site.baseurl }}{% link api-reference/configuration/add-effect-workflow-bus.md %}) | Registers the WorkflowBus and discovers workflows via assembly scanning |
| [AddEffect / AddStepEffect]({{ site.baseurl }}{% link api-reference/configuration/add-effect.md %}) | Registers custom effect provider factories |
| [SetEffectLogLevel]({{ site.baseurl }}{% link api-reference/configuration/set-effect-log-level.md %}) | Sets the minimum log level for effect logging |
