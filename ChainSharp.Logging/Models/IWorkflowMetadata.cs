using System.ComponentModel.DataAnnotations.Schema;
using ChainSharp.Logging.Enums;

namespace ChainSharp.Logging.Models;

public interface IWorkflowMetadata
{
    [Column("id")]
    public int Id { get; }

    [Column("external_id")]
    public string ExternalId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("executor")]
    public string? Executor { get; }

    [Column("workflow_state")]
    public WorkflowState WorkflowState { get; set; }

    [Column("changes")]
    public int Changes { get; set; }

    [Column("failure_step")]
    public string? FailureStep { get; }
    
    [Column("failure_exception")]
    public string? FailureException { get; }

    [Column("failure_reason")]
    public string? FailureReason { get; }

    [Column("stack_trace")]
    public string? StackTrace { get; set; }
    
    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }
}