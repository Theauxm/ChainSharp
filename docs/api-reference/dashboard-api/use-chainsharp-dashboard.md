---
layout: default
title: UseChainSharpDashboard
parent: Dashboard API
grand_parent: API Reference
nav_order: 2
---

# UseChainSharpDashboard

Maps the ChainSharp Dashboard Blazor components at the configured route prefix. Call this after `app.Build()` during application startup.

## Signature

```csharp
public static WebApplication UseChainSharpDashboard(
    this WebApplication app,
    string routePrefix = "/chainsharp",
    string? title = null
)
```

## Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `routePrefix` | `string` | No | `"/chainsharp"` | URL prefix where the dashboard is mounted. Leading/trailing slashes are normalized. |
| `title` | `string?` | No | `null` | Overrides the dashboard title. `null` keeps the title from [DashboardOptions]({{ site.baseurl }}{% link api-reference/dashboard-api/dashboard-options.md %}). |

## Returns

`WebApplication` — for continued middleware chaining.

## Example

```csharp
var app = builder.Build();

app.UseChainSharpDashboard(
    routePrefix: "/admin/workflows",
    title: "Order Processing Dashboard");

app.Run();
```

The dashboard will be accessible at `https://yourapp/admin/workflows`.

## What It Configures

1. `UseStaticFiles()` — serves static assets (CSS, JS)
2. `UseAntiforgery()` — CSRF protection for Blazor forms
3. `MapStaticAssets()` — maps static web assets from the dashboard RCL
4. `MapRazorComponents<App>().AddInteractiveServerRenderMode()` — maps Blazor components with Interactive Server rendering

## Remarks

- Must be called **after** `builder.Build()` and **before** `app.Run()`.
- The `routePrefix` is normalized: `"chainsharp"`, `"/chainsharp"`, and `"/chainsharp/"` all resolve to `"/chainsharp"`.
- The dashboard requires a data provider ([AddPostgresEffect]({{ site.baseurl }}{% link api-reference/configuration/add-postgres-effect.md %}) or [AddInMemoryEffect]({{ site.baseurl }}{% link api-reference/configuration/add-in-memory-effect.md %})) to be configured for metadata and manifest pages to function.
