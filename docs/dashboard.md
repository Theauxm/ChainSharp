---
layout: default
title: Dashboard
nav_order: 6
---

# Dashboard

ChainSharp.Effect.Dashboard adds a web UI to your application for inspecting registered workflows. It mounts as a Blazor Server app at a route you choose—similar to how Hangfire's dashboard works at `/hangfire`.

The dashboard only requires `ChainSharp.Effect`. As you add more Effect packages (Data, Scheduler, etc.), the dashboard gains access to more information. Start with workflow discovery, and the rest follows naturally as your setup grows.

## Quick Setup

### Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect.Dashboard
```

Or in your `.csproj`:

```xml
<PackageReference Include="Theauxm.ChainSharp.Effect.Dashboard" Version="5.*" />
```

### Configuration

Two lines in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChainSharpEffects(o => o.AddEffectWorkflowBus(typeof(Program).Assembly));
builder.Services.AddChainSharpDashboard();

var app = builder.Build();

app.UseChainSharpDashboard("/chainsharp");

app.Run();
```

Navigate to `/chainsharp/workflows` and you'll see every `IEffectWorkflow` registered in your application.

## What It Shows

### Workflows Page

The dashboard scans your DI container for all services implementing `IEffectWorkflow<TIn, TOut>` and displays them in a sortable, filterable grid:

| Column | Description |
|--------|-------------|
| **Workflow** | The service interface name (e.g., `ICreateUserWorkflow`) |
| **Implementation** | The concrete class (e.g., `CreateUserWorkflow`) |
| **Input Type** | The `TIn` generic argument |
| **Output Type** | The `TOut` generic argument |
| **Lifetime** | DI lifetime—Transient, Scoped, or Singleton |

This is the same information the `WorkflowRegistry` uses internally, but surfaced in a UI instead of buried in reflection.

## How Discovery Works

The dashboard reads from the same `IServiceCollection` that your application builds. When you call `AddChainSharpDashboard()`, it captures a reference to the service collection. At request time, it scans the registered `ServiceDescriptor` entries for anything that implements `IEffectWorkflow<,>`, extracts the generic type arguments, and deduplicates.

If you register workflows with `AddEffectWorkflowBus` (which calls `AddScopedChainSharpWorkflow` under the hood), they show up automatically. Workflows registered manually via `AddScoped<IMyWorkflow, MyWorkflow>()` will also appear as long as their interface extends `IEffectWorkflow<TIn, TOut>`.

## Options

```csharp
builder.Services.AddChainSharpDashboard(options =>
{
    options.Title = "My App";  // Header text (default: "ChainSharp")
});
```

The route prefix is set in `UseChainSharpDashboard`:

```csharp
app.UseChainSharpDashboard("/admin/chainsharp");
```

## Layout

The dashboard uses [Radzen Blazor](https://blazor.radzen.com/) components with a sidebar navigation layout. The sidebar is designed for multiple pages—right now there's only Workflows, but as more Effect packages are integrated, pages for metadata history, scheduler status, and dead letter management will follow.

## Integration with Existing Blazor Apps

If your application already uses Blazor Server, the dashboard's `AddChainSharpDashboard()` call is safe to use alongside your existing `AddRazorComponents()`. The dashboard pages use their own layout, so they won't interfere with your app's UI.

If your application is a minimal API or MVC app that doesn't use Blazor, the dashboard adds the necessary Blazor Server infrastructure automatically.

## Architecture

The dashboard sits alongside other Effect packages in the dependency tree:

```
ChainSharp.Effect
    ├── ChainSharp.Effect.Dashboard (UI)
    ├── ChainSharp.Effect.Mediator (WorkflowBus)
    ├── ChainSharp.Effect.Data (Persistence)
    └── ...
```

It depends only on `ChainSharp.Effect`—no transitive dependency on Data, Mediator, Scheduler, or any database provider. The dashboard discovers what's available in your DI container and adapts accordingly.
