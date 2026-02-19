namespace ChainSharp.Effect.Dashboard.Models;

public class DagNode
{
    public int Id { get; init; }
    public string Label { get; init; } = "";
    public bool IsHighlighted { get; init; }
}

public class DagEdge
{
    /// <summary>
    /// Upstream node ID (the dependency/parent — rendered on the left).
    /// </summary>
    public int FromId { get; init; }

    /// <summary>
    /// Downstream node ID (the dependent — rendered on the right).
    /// </summary>
    public int ToId { get; init; }
}
