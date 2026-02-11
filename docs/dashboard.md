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

## Troubleshooting

### "Page doesn't load" or blank screen

`UseChainSharpDashboard()` needs to be called after `builder.Build()` and before `app.Run()`. If it's missing or misordered, the Blazor endpoints won't be mapped.

```csharp
var app = builder.Build();

app.UseChainSharpDashboard("/chainsharp");  // After Build(), before Run()

app.Run();
```

### "No workflows listed"

The dashboard discovers workflows by scanning `ServiceDescriptor` entries in your DI container. If the grid is empty:

**Causes:**
- The assembly containing your workflows wasn't passed to `AddEffectWorkflowBus`
- `AddChainSharpDashboard()` was called before the workflows were registered, so the captured `IServiceCollection` snapshot doesn't include them yet

**Fix:** Make sure `AddChainSharpDashboard()` is called after `AddChainSharpEffects`:

```csharp
builder.Services.AddChainSharpEffects(o =>
    o.AddEffectWorkflowBus(typeof(Program).Assembly)
);
builder.Services.AddChainSharpDashboard();  // After workflows are registered
```

### Blazor static assets returning 404

If styles are missing or `_content/` paths return 404, ensure `UseStaticFiles()` is in your middleware pipeline. `UseChainSharpDashboard()` calls it internally, but if something earlier in the pipeline is short-circuiting requests, the static file middleware might not run.

### Duplicate workflow entries in the grid

`AddScopedChainSharpWorkflow<IMyWorkflow, MyWorkflow>()` registers two DI descriptors—one for the concrete type and one for the interface. The discovery service attempts to deduplicate these, but in some cases both registrations appear in the grid. The entries will have the same input/output types; one will show the interface name and the other the concrete class name.

This is cosmetic and doesn't affect workflow execution. If it bothers you, it's a known limitation of how the discovery service groups factory-based descriptors.

## Architecture

The dashboard sits alongside other Effect packages in the dependency tree:

```
ChainSharp.Effect
    ├── ChainSharp.Effect.Dashboard (UI)
    ├── ChainSharp.Effect.Orchestration.Mediator (WorkflowBus)
    ├── ChainSharp.Effect.Data (Persistence)
    └── ...
```

It depends only on `ChainSharp.Effect`—no transitive dependency on Data, Mediator, Scheduler, or any database provider. The dashboard discovers what's available in your DI container and adapts accordingly.
