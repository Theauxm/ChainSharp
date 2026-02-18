---
layout: default
title: SetEffectLogLevel
parent: Configuration
grand_parent: API Reference
nav_order: 9
---

# SetEffectLogLevel

Sets the minimum log level for ChainSharp effect logging.

## Signature

```csharp
public static ChainSharpEffectConfigurationBuilder SetEffectLogLevel(
    this ChainSharpEffectConfigurationBuilder builder,
    LogLevel logLevel
)
```

## Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `logLevel` | `LogLevel` | Yes | The minimum log level for effect logging (e.g., `LogLevel.Information`, `LogLevel.Warning`) |

## Returns

`ChainSharpEffectConfigurationBuilder` — for continued fluent chaining.

## Example

```csharp
services.AddChainSharpEffects(options => options
    .SetEffectLogLevel(LogLevel.Warning)
    .AddPostgresEffect(connectionString)
);
```

## Remarks

- The default log level is `LogLevel.Debug`.
- This controls the log level for the ChainSharp effect system's own logging, not for the data context logging (which is controlled by [AddEffectDataContextLogging]({% link api-reference/configuration/add-effect-data-context-logging.md %})).
