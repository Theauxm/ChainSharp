using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using ChainSharp.Effect.Enums;

namespace ChainSharp.Effect.Models.Metadata;

public interface IMetadata : IModel
{
    [Column("external_id")]
    public string ExternalId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("executor")]
    public string? Executor { get; }

    [Column("workflow_state")]
    public WorkflowState WorkflowState { get; set; }

    [Column("failure_step")]
    public string? FailureStep { get; }

    [Column("failure_exception")]
    public string? FailureException { get; }

    [Column("failure_reason")]
    public string? FailureReason { get; }

    [Column("stack_trace")]
    public string? StackTrace { get; set; }

    [Column("input")]
    public JsonDocument? Input { get; set; }

    [Column("output")]
    public JsonDocument? Output { get; set; }

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }
}
