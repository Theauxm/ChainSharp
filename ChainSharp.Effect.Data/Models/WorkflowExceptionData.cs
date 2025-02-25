using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Data.Models;

/// <summary>
/// Object for an exception that occurs
/// within a step of a workflow.
/// </summary>
public class WorkflowExceptionData
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("step")]
    public string Step { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}
