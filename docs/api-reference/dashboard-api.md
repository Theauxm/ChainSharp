---
layout: default
title: Dashboard API
parent: API Reference
nav_order: 5
has_children: true
---

# Dashboard API

The ChainSharp dashboard is a Blazor Server UI that provides real-time visibility into workflow execution, metadata, manifests, and dead letters. It's distributed as a Razor Class Library and mounted into your existing ASP.NET Core application.

```csharp
// In Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddChainSharpDashboard();

var app = builder.Build();
app.UseChainSharpDashboard();
```

| Page | Description |
|------|-------------|
| [AddChainSharpDashboard]({{ site.baseurl }}{% link api-reference/dashboard-api/add-chainsharp-dashboard.md %}) | Registers dashboard services (Blazor, Radzen, workflow discovery) |
| [UseChainSharpDashboard]({{ site.baseurl }}{% link api-reference/dashboard-api/use-chainsharp-dashboard.md %}) | Maps the dashboard Blazor components at a route prefix |
| [DashboardOptions]({{ site.baseurl }}{% link api-reference/dashboard-api/dashboard-options.md %}) | Configuration options for route prefix, title, and environment |
