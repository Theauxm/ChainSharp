using System.Text.Json.Serialization;

namespace ChainSharp.Effect.Models;

/// <summary>
/// Represents structured data about an exception that occurs within a workflow step.
/// This class is used to serialize and deserialize exception information for storage
/// and analysis.
/// </summary>
/// <remarks>
/// The WorkflowExceptionData class provides a standardized format for capturing
/// exception details in the ChainSharp.Effect system. This information is stored
/// in the workflow metadata and can be used for:
///
/// 1. Debugging workflow failures
/// 2. Analyzing patterns of failures across workflows
/// 3. Generating reports on workflow reliability
/// 4. Implementing retry or compensation logic based on specific failure types
///
/// When a workflow fails, the exception information is extracted and stored in
/// this format, making it easier to query and analyze than raw exception data.
/// </remarks>
public class WorkflowExceptionData
{
    /// <summary>
    /// Gets or sets the type of the exception that occurred.
    /// </summary>
    /// <remarks>
    /// This property typically contains the fully qualified name of the exception class,
    /// such as "System.ArgumentNullException" or "ChainSharp.Exceptions.WorkflowException".
    ///
    /// This information is useful for categorizing exceptions and implementing
    /// specific handling logic for different exception types.
    /// </remarks>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow step where the exception occurred.
    /// </summary>
    /// <remarks>
    /// This property identifies the specific step in the workflow that failed,
    /// making it easier to locate the source of the error.
    ///
    /// For example, if a workflow has steps like "ValidateInput", "ProcessData",
    /// and "SaveResults", this property would indicate which of these steps
    /// encountered the exception.
    /// </remarks>
    [JsonPropertyName("step")]
    public string Step { get; set; }

    /// <summary>
    /// Gets or sets the error message associated with the exception.
    /// </summary>
    /// <remarks>
    /// This property contains the human-readable description of what went wrong,
    /// typically derived from the Exception.Message property.
    ///
    /// The message should provide enough detail to understand the nature of the
    /// error without requiring access to the original exception object.
    /// </remarks>
    [JsonPropertyName("message")]
    public string Message { get; set; }
}
