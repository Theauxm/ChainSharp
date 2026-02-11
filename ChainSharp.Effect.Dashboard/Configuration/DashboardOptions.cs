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
}
