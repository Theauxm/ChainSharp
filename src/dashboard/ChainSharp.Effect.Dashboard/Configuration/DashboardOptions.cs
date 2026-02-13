namespace ChainSharp.Effect.Dashboard.Configuration;

public class DashboardOptions
{
    /// <summary>
    /// The URL prefix where the dashboard is mounted (e.g., "/chainsharp").
    /// </summary>
    public string RoutePrefix { get; set; } = "/chainsharp";

    /// <summary>
    /// Title displayed in the dashboard header.
    /// </summary>
    public string Title { get; set; } = "ChainSharp";

    /// <summary>
    /// The hosting environment name (e.g., "Development", "Production").
    /// Automatically populated from <c>IHostEnvironment.EnvironmentName</c>
    /// when <see cref="Extensions.DashboardServiceExtensions.UseChainSharpDashboard"/> is called.
    /// </summary>
    public string EnvironmentName { get; set; } = "";
}
