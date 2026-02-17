namespace ChainSharp.Effect.Dashboard.Models;

public class GroupSummary
{
    public string GroupId { get; init; } = "";
    public int ManifestCount { get; init; }
    public int TotalExecutions { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public DateTime? LastRun { get; init; }
}
