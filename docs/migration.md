---
layout: default
title: Migration (4.x → 5.x)
nav_order: 11
---

# Migration from 4.x to 5.x

## Target Framework

ChainSharp 5.x targets `net10.0` exclusively. If your project is on `net8.0` or `net9.0`, you'll need to update your target framework before upgrading. Version 4.x is the last release supporting `net8.0`.

```xml
<!-- Before -->
<TargetFramework>net8.0</TargetFramework>

<!-- After -->
<TargetFramework>net10.0</TargetFramework>
```

This affects your entire solution—every project that references a ChainSharp 5.x package must target `net10.0`.

## Package Renames

Several packages were reorganized to reflect the layered architecture. The old package names no longer exist on NuGet:

| Old Package (4.x) | New Package (5.x) |
|---|---|
| `Theauxm.ChainSharp.Effect.Json` | `Theauxm.ChainSharp.Effect.Provider.Json` |
| `Theauxm.ChainSharp.Effect.Mediator` | `Theauxm.ChainSharp.Effect.Orchestration.Mediator` |
| `Theauxm.ChainSharp.Effect.Parameter` | `Theauxm.ChainSharp.Effect.Provider.Parameter` |

## Namespace Changes

The `using` statements follow the new package names. Find-and-replace works for most of these:

| Old Namespace (4.x) | New Namespace (5.x) |
|---|---|
| `ChainSharp.Effect.Json` | `ChainSharp.Effect.Provider.Json` |
| `ChainSharp.Effect.Mediator` | `ChainSharp.Effect.Orchestration.Mediator` |
| `ChainSharp.Effect.Parameter` | `ChainSharp.Effect.Provider.Parameter` |

## Step-by-Step Upgrade

1. **Update your TFM** to `net10.0` across all projects in the solution.
2. **Replace package references.** Swap the old package names for the new ones in every `.csproj` file.
3. **Update `using` statements.** A global find-and-replace across `.cs` files covers it.
4. **Restore and build.** `dotnet restore && dotnet build` will surface anything you missed.

If you hit unresolved types after the rename, check the [Namespace Reference](scheduler/setup.md#namespace-reference) for the full namespace of scheduler-related types—some live in unexpected packages.
