using ChainSharp.Effect.Dashboard.Components;
using ChainSharp.Effect.Dashboard.Configuration;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace ChainSharp.Effect.Dashboard.Extensions;

public static class DashboardServiceExtensions
{
    /// <summary>
    /// Registers ChainSharp Dashboard services including Radzen components and workflow discovery.
    /// </summary>
    public static IServiceCollection AddChainSharpDashboard(
        this IServiceCollection services,
        Action<DashboardOptions>? configure = null
    )
    {
        var options = new DashboardOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Capture IServiceCollection so WorkflowDiscoveryService can scan descriptors at runtime
        services.AddSingleton<IServiceCollection>(services);

        services.AddScoped<IWorkflowDiscoveryService, WorkflowDiscoveryService>();

        services.AddRadzenComponents();

        services.AddRazorComponents().AddInteractiveServerComponents();

        return services;
    }

    /// <summary>
    /// Maps the ChainSharp Dashboard Blazor components at the configured route prefix.
    /// </summary>
    public static WebApplication UseChainSharpDashboard(
        this WebApplication app,
        string routePrefix = "/chainsharp"
    )
    {
        routePrefix = "/" + routePrefix.Trim('/');

        var options = app.Services.GetRequiredService<DashboardOptions>();
        options.RoutePrefix = routePrefix;

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        return app;
    }
}
