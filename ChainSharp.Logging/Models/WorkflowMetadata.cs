using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json;
using ChainSharp.Exceptions;
using ChainSharp.Logging.Enums;
using ChainSharp.Logging.Extensions;
using ChainSharp.Logging.Services.ChainSharpProvider;
using LanguageExt;

namespace ChainSharp.Logging.Models;

public class WorkflowMetadata : IWorkflowMetadata
{
    [Column("id")]
    public int Id { get; private set; }

    [Column("external_id")]
    public string ExternalId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("executor")]
    public string? Executor { get; private set; }

    [Column("workflow_state")]
    public WorkflowState WorkflowState { get; set; }

    [Column("changes")]
    public int Changes { get; set; }

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

    private WorkflowMetadata() {}

    public static WorkflowMetadata Create(IChainSharpProvider provider, string workflowName)
    {
        var newWorkflow = new WorkflowMetadata
        {
            Name = workflowName,
            WorkflowState = WorkflowState.Pending,
            Executor = Assembly.GetEntryAssembly()?.GetAssemblyProject(),
            Changes = 0,
            StartTime = DateTime.UtcNow
        };

        provider.Track(newWorkflow);

        return newWorkflow;
    }

    public Unit AddException(Exception workflowException)
    {
        try
        {
            var deserializedException = JsonSerializer.Deserialize<WorkflowExceptionData>(workflowException.Message);

            if (deserializedException == null)
                throw new Exception($"Could not deserialize exception data from ({Name}) workflow.");
            
            FailureException = deserializedException.Type;
            FailureReason = deserializedException.Message;
            FailureStep = deserializedException.Step;
            StackTrace = workflowException.StackTrace;
            
            return Unit.Default;
        }
        catch (Exception e)
        {
            throw new WorkflowException($"Found Exception Type ({e.GetType()}) with Message ({e.Message}). Could not deserialize exception data from ({Name}) workflow. This is likely because this function was not correctly called or the RailwayStep function did not process the exception Message correctly.");
        }
    }
}