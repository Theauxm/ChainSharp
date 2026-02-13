---
layout: default
title: Parameter Effect
parent: Effect Providers
grand_parent: Usage Guide
nav_order: 3
---

# Parameter Effect

The parameter effect serializes workflow inputs and outputs to JSON and stores them on the `Metadata` record. Without this provider, the `Metadata.Input` and `Metadata.Output` columns are null—you'll know a workflow ran and whether it succeeded, but not what data it processed.

## Registration

```bash
dotnet add package Theauxm.ChainSharp.Effect.Provider.Parameter
```

```csharp
services.AddChainSharpEffects(options =>
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()
);
```

You can pass custom serialization options:

```csharp
.SaveWorkflowParameters(new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
})
```

## How It Works

The parameter effect only cares about `Metadata` objects—it ignores other tracked models. When `SaveChanges` runs:

1. It iterates through every tracked `Metadata` instance.
2. It calls `metadata.GetInputObject()` and `metadata.GetOutputObject()` to retrieve the raw workflow input and output.
3. It serializes both to JSON strings and assigns them to `metadata.Input` and `metadata.Output`.

These fields are then persisted by whatever data provider you have registered (Postgres or InMemory). When you later inspect workflow executions—through the [Dashboard](../../dashboard.md), direct database queries, or the metadata API—you can see exactly what went in and what came out.

On disposal, the provider clears the input/output object references from metadata to release memory.

## Requires a Data Provider

This effect populates fields on `Metadata`, but it doesn't persist the metadata itself. You need either `AddPostgresEffect` or `AddInMemoryEffect` registered alongside it. Without a data provider, the serialized parameters are written to a `Metadata` object that's never saved anywhere.

## When to Use It

- **Production** — When you need to query or debug workflow executions after the fact. "What input caused this failure?"
- **Audit trails** — The serialized input/output gives you a record of what data each workflow processed.
- **Dashboard** — The [Dashboard](../../dashboard.md) displays `Input` and `Output` in its metadata detail view. Without this provider, those fields show as empty.
