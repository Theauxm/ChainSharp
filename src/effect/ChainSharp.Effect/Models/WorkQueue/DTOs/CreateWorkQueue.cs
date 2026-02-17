namespace ChainSharp.Effect.Models.WorkQueue.DTOs;

/// <summary>
/// Data transfer object for creating a new WorkQueue entry.
/// </summary>
public class CreateWorkQueue
{
    /// <summary>
    /// The fully qualified workflow type name to execute.
    /// </summary>
    public required string WorkflowName { get; set; }

    /// <summary>
    /// Serialized workflow input (JSON). Same format as Manifest.Properties.
    /// </summary>
    public string? Input { get; set; }

    /// <summary>
    /// Fully qualified type name of the input, for deserialization.
    /// </summary>
    public string? InputTypeName { get; set; }

    /// <summary>
    /// Optional manifest ID when this entry was queued from a scheduled manifest.
    /// </summary>
    public int? ManifestId { get; set; }
}
