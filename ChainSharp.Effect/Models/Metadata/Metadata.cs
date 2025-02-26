using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Exceptions;
using LanguageExt;

namespace ChainSharp.Effect.Models.Metadata;

public class Metadata : IMetadata
{
    #region Columns

    [Column("id")]
    [JsonPropertyName("id")]
    public int Id { get; private set; }

    [Column("parent_id")]
    [JsonPropertyName("parent_id")]
    [JsonInclude]
    public int? ParentId { get; set; }

    [Column("external_id")]
    public string ExternalId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("executor")]
    public string? Executor { get; private set; }

    [Column("workflow_state")]
    public WorkflowState WorkflowState { get; set; }

    [Column("failure_step")]
    public string? FailureStep { get; private set; }

    [Column("failure_exception")]
    public string? FailureException { get; private set; }

    [Column("failure_reason")]
    public string? FailureReason { get; private set; }

    [Column("stack_trace")]
    public string? StackTrace { get; set; }

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    public bool IsChild => ParentId is not null;

    #endregion

    #region ForeignKeys

    public Metadata Parent { get; private set; }

    public ICollection<Metadata> Children { get; private set; }

    #endregion

    #region Functions

    public static Metadata Create(CreateMetadata metadata)
    {
        var newWorkflow = new Metadata
        {
            Name = metadata.Name,
            ExternalId = Guid.NewGuid().ToString("N"),
            WorkflowState = WorkflowState.Pending,
            Executor = Assembly.GetEntryAssembly()?.GetAssemblyProject(),
            StartTime = DateTime.UtcNow
        };

        return newWorkflow;
    }

    public Unit AddException(Exception workflowException)
    {
        try
        {
            var deserializedException = JsonSerializer.Deserialize<WorkflowExceptionData>(
                workflowException.Message
            );

            if (deserializedException == null)
                throw new Exception(
                    $"Could not deserialize exception data from ({Name}) workflow."
                );

            FailureException = deserializedException.Type;
            FailureReason = deserializedException.Message;
            FailureStep = deserializedException.Step;
            StackTrace = workflowException.StackTrace;

            return Unit.Default;
        }
        catch (Exception e)
        {
            throw new WorkflowException(
                $"Found Exception Type ({e.GetType()}) with Message ({e.Message}). Could not deserialize exception data from ({Name}) workflow. This is likely because this function was not correctly called or the RailwayStep function did not process the exception Message correctly."
            );
        }
    }

    #endregion

    [JsonConstructor]
    public Metadata() { }
}
