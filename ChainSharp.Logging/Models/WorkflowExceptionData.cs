using System.Text.Json.Serialization;

namespace ChainSharp.Logging.Models;

public class WorkflowExceptionData
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("step")]
    public string Step { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}
