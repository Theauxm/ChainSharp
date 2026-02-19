---
layout: default
title: DashboardOptions
parent: Dashboard API
grand_parent: API Reference
nav_order: 3
---

# DashboardOptions

Configuration class for the ChainSharp Dashboard. Passed via the `configure` callback in [AddChainSharpDashboard]({{ site.baseurl }}{% link api-reference/dashboard-api/add-chainsharp-dashboard.md %}).

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RoutePrefix` | `string` | `"/chainsharp"` | URL prefix where the dashboard is mounted. Set automatically by [UseChainSharpDashboard]({{ site.baseurl }}{% link api-reference/dashboard-api/use-chainsharp-dashboard.md %}). |
| `Title` | `string` | `"ChainSharp"` | Title displayed in the dashboard header and browser tab. |
| `EnvironmentName` | `string` | `""` | The hosting environment name (e.g., "Development", "Production"). Auto-populated by `UseChainSharpDashboard`. |

## Example

```csharp
builder.AddChainSharpDashboard(options =>
{
    options.Title = "My Application - Workflows";
});

app.UseChainSharpDashboard(routePrefix: "/workflows");
// RoutePrefix is set to "/workflows" by UseChainSharpDashboard
// EnvironmentName is set automatically from the hosting environment
```

## Remarks

- `RoutePrefix` and `EnvironmentName` are typically set by `UseChainSharpDashboard`, not in the `configure` callback. Setting them in `configure` will be overwritten.
- `Title` is the only property typically set by users in the `configure` callback.
