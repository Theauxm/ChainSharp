using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using ChainSharp.Effect.Enums;
using LanguageExt;

namespace ChainSharp.Effect.Models.Metadata;

/// <summary>
/// Defines the contract for workflow metadata entities in the ChainSharp.Effect system.
/// This interface extends IModel to add workflow-specific tracking properties.
/// </summary>
/// <remarks>
/// The IMetadata interface represents the core tracking entity for workflows in the system.
/// It captures all relevant information about a workflow execution, including:
///
/// 1. Basic identification (Id, ExternalId, Name)
/// 2. Execution context (Executor)
/// 3. State information (WorkflowState)
/// 4. Error details (FailureStep, FailureException, FailureReason, StackTrace)
/// 5. Input and output data (Input, Output)
/// 6. Timing information (StartTime, EndTime)
///
/// This comprehensive tracking enables detailed analysis, monitoring, and debugging
/// of workflow executions across the system.
/// </remarks>
public interface IMetadata : IModel
{
    /// <summary>
    /// Gets or sets a globally unique identifier for the workflow execution.
    /// </summary>
    /// <remarks>
    /// The ExternalId is typically a GUID that can be used to reference the workflow
    /// from external systems. Unlike the database Id, this identifier is designed
    /// to be shared across system boundaries.
    /// </remarks>
    [Column("external_id")]
    string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow.
    /// </summary>
    /// <remarks>
    /// The Name typically corresponds to the class name of the workflow implementation.
    /// This provides a human-readable identifier for the workflow type.
    /// </remarks>
    [Column("name")]
    string Name { get; set; }

    /// <summary>
    /// Gets the name of the assembly that executed the workflow.
    /// </summary>
    /// <remarks>
    /// The Executor identifies the source of the workflow execution, which is useful
    /// in distributed systems where workflows might be executed by different services.
    /// </remarks>
    [Column("executor")]
    string? Executor { get; }

    /// <summary>
    /// Gets or sets the current state of the workflow.
    /// </summary>
    /// <remarks>
    /// The WorkflowState tracks the lifecycle of the workflow execution,
    /// from Pending through InProgress to either Completed or Failed.
    /// This is a key property for monitoring and reporting on workflow status.
    /// </remarks>
    [Column("workflow_state")]
    WorkflowState WorkflowState { get; set; }

    /// <summary>
    /// Gets the name of the step where the workflow failed, if applicable.
    /// </summary>
    /// <remarks>
    /// When a workflow fails, this property identifies the specific step
    /// that encountered the error, making it easier to diagnose issues.
    /// </remarks>
    [Column("failure_step")]
    string? FailureStep { get; }

    /// <summary>
    /// Gets the type of exception that caused the workflow to fail, if applicable.
    /// </summary>
    /// <remarks>
    /// This property stores the fully qualified name of the exception class
    /// that caused the workflow failure, enabling categorization of errors.
    /// </remarks>
    [Column("failure_exception")]
    string? FailureException { get; }

    /// <summary>
    /// Gets the error message associated with the workflow failure, if applicable.
    /// </summary>
    /// <remarks>
    /// This property contains the human-readable description of what went wrong,
    /// typically derived from the Exception.Message property.
    /// </remarks>
    [Column("failure_reason")]
    string? FailureReason { get; }

    /// <summary>
    /// Gets or sets the stack trace associated with the workflow failure, if applicable.
    /// </summary>
    /// <remarks>
    /// The stack trace provides detailed information about the sequence of method calls
    /// that led to the exception, which is valuable for debugging complex issues.
    /// </remarks>
    [Column("stack_trace")]
    string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the serialized input data for the workflow.
    /// </summary>
    /// <remarks>
    /// The Input property stores the serialized form of the data that was provided
    /// to the workflow when it was executed. This is useful for reproducing issues
    /// and understanding the context of the workflow execution.
    /// </remarks>
    [Column("input")]
    JsonDocument? Input { get; set; }

    /// <summary>
    /// Gets or sets the serialized output data from the workflow.
    /// </summary>
    /// <remarks>
    /// The Output property stores the serialized form of the data that was produced
    /// by the workflow when it completed successfully. This allows for analysis of
    /// workflow results and verification of expected outcomes.
    /// </remarks>
    [Column("output")]
    JsonDocument? Output { get; set; }

    /// <summary>
    /// Gets or sets the time when the workflow execution started.
    /// </summary>
    /// <remarks>
    /// The StartTime is recorded when the workflow is initialized and begins execution.
    /// This is used for tracking execution duration and for time-based analysis.
    /// </remarks>
    [Column("start_time")]
    DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the time when the workflow execution completed or failed.
    /// </summary>
    /// <remarks>
    /// The EndTime is recorded when the workflow reaches a terminal state (Completed or Failed).
    /// This property, along with StartTime, allows for calculation of execution duration
    /// and identification of long-running workflows.
    /// </remarks>
    [Column("end_time")]
    DateTime? EndTime { get; set; }

    /// <summary>
    /// Adds exception details to this metadata record.
    /// </summary>
    /// <param name="workflowException">The exception that caused the workflow to fail</param>
    /// <returns>A Unit value (similar to void, but functional)</returns>
    /// <remarks>
    /// This method extracts information from the provided exception and populates
    /// the failure-related properties of the metadata record. It attempts to deserialize
    /// the exception message as a WorkflowExceptionData object, which provides structured
    /// information about the failure. If deserialization fails, it falls back to extracting
    /// information directly from the exception.
    ///
    /// The method sets:
    /// 1. FailureException - The type of the exception
    /// 2. FailureReason - The error message
    /// 3. FailureStep - The step where the failure occurred
    /// 4. StackTrace - The stack trace of the exception
    ///
    /// This information is valuable for diagnosing and analyzing workflow failures.
    /// </remarks>
    Unit AddException(Exception workflowException);
}
