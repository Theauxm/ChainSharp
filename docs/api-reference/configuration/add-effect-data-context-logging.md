---
layout: default
title: AddEffectDataContextLogging
parent: Configuration
grand_parent: API Reference
nav_order: 3
---

# AddEffectDataContextLogging

Enables logging for database operations. Captures SQL queries, transaction boundaries, and errors into the ChainSharp logging pipeline.

## Signature

```csharp
public static ChainSharpEffectConfigurationBuilder AddEffectDataContextLogging(
    this ChainSharpEffectConfigurationBuilder configurationBuilder,
    LogLevel? minimumLogLevel = null,
    List<string>? blacklist = null
)
```

## Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `minimumLogLevel` | `LogLevel?` | No | `LogLevel.Information` | Minimum log level to capture. Can be overridden by the `CHAIN_SHARP_POSTGRES_LOG_LEVEL` environment variable. |
| `blacklist` | `List<string>?` | No | `[]` (empty) | Namespace patterns to exclude from logging (e.g., `["Microsoft.EntityFrameworkCore.*"]`) |

## Returns

`ChainSharpEffectConfigurationBuilder` — for continued fluent chaining.

## Example

```csharp
services.AddChainSharpEffects(options => options
    .AddPostgresEffect(connectionString)
    .AddEffectDataContextLogging(
        minimumLogLevel: LogLevel.Warning,
        blacklist: ["Microsoft.EntityFrameworkCore.Database.Command"])
);
```

## Remarks

- **Must** be called after a data provider ([AddPostgresEffect]({{ site.baseurl }}{% link api-reference/configuration/add-postgres-effect.md %}) or [AddInMemoryEffect]({{ site.baseurl }}{% link api-reference/configuration/add-in-memory-effect.md %})). Throws an `Exception` if no data provider is registered.
- The `CHAIN_SHARP_POSTGRES_LOG_LEVEL` environment variable takes precedence over the `minimumLogLevel` parameter if set.
- Registers `DataContextLoggingProvider` as an `ILoggerProvider`.
