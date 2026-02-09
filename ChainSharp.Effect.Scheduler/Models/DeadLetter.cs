using System.ComponentModel.DataAnnotations.Schema;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Scheduler.Models;

/// <summary>
/// Represents a job execution that has been moved to the dead letter queue after exceeding retry limits.
/// </summary>
/// <remarks>
/// Dead letters are created when a job has failed too many times and requires manual intervention.
/// They serve as both an audit trail and a mechanism for operators to review, retry, or acknowledge
/// failed jobs.
/// 
/// A dead letter maintains a reference to its original Metadata record, preserving the full
/// execution history including the failure details, stack traces, and input/output data.
/// </remarks>
public class DeadLetter : IModel
{
    #region Columns

    /// <summary>
    /// Gets the unique identifier for this dead letter record.
    /// </summary>
    [Column("id")]
    public int Id { get; private set; }

    /// <summary>
    /// Gets the time when this job was moved to the dead letter queue.
    /// </summary>
    [Column("dead_lettered_at")]
    public DateTime DeadLetteredAt { get; private set; }

    /// <summary>
    /// Gets the reason why this job was dead-lettered.
    /// </summary>
    /// <remarks>
    /// This could be "Max retries exceeded", "Non-retryable exception", 
    /// "Manual dead-letter by operator", etc.
    /// </remarks>
    [Column("reason")]
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current resolution status of this dead letter.
    /// </summary>
    [Column("status")]
    public DeadLetterStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the time when this dead letter was resolved (retried or acknowledged).
    /// </summary>
    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Gets or sets a note explaining the resolution (e.g., why it was acknowledged).
    /// </summary>
    [Column("resolution_note")]
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// Gets the total number of retry attempts before dead-lettering.
    /// </summary>
    [Column("retry_count_at_dead_letter")]
    public int RetryCountAtDeadLetter { get; private set; }

    #endregion

    #region ForeignKeys

    /// <summary>
    /// Gets the ID of the associated Metadata record.
    /// </summary>
    [Column("metadata_id")]
    public int MetadataId { get; private set; }

    /// <summary>
    /// Gets the associated Metadata record containing the execution details.
    /// </summary>
    public Metadata? Metadata { get; private set; }

    /// <summary>
    /// Gets or sets the ID of the retry Metadata record, if this dead letter was retried.
    /// </summary>
    [Column("retry_metadata_id")]
    public int? RetryMetadataId { get; set; }

    /// <summary>
    /// Gets the Metadata record for the retry execution, if applicable.
    /// </summary>
    public Metadata? RetryMetadata { get; private set; }

    #endregion

    #region Functions

    /// <summary>
    /// Creates a new DeadLetter record for a failed job execution.
    /// </summary>
    /// <param name="metadata">The failed Metadata record</param>
    /// <param name="reason">The reason for dead-lettering</param>
    /// <param name="retryCount">The number of retries attempted</param>
    /// <returns>A new DeadLetter instance</returns>
    public static DeadLetter Create(Metadata metadata, string reason, int retryCount)
    {
        return new DeadLetter
        {
            MetadataId = metadata.Id,
            Metadata = metadata,
            DeadLetteredAt = DateTime.UtcNow,
            Reason = reason,
            Status = DeadLetterStatus.AwaitingIntervention,
            RetryCountAtDeadLetter = retryCount
        };
    }

    /// <summary>
    /// Marks this dead letter as acknowledged without retrying.
    /// </summary>
    /// <param name="note">A note explaining the acknowledgement</param>
    public void Acknowledge(string note)
    {
        Status = DeadLetterStatus.Acknowledged;
        ResolvedAt = DateTime.UtcNow;
        ResolutionNote = note;
    }

    /// <summary>
    /// Marks this dead letter as retried.
    /// </summary>
    /// <param name="retryMetadataId">The ID of the new Metadata record for the retry</param>
    public void MarkRetried(int retryMetadataId)
    {
        Status = DeadLetterStatus.Retried;
        ResolvedAt = DateTime.UtcNow;
        RetryMetadataId = retryMetadataId;
    }

    #endregion

    private DeadLetter() { }
}

/// <summary>
/// The resolution status of a dead letter.
/// </summary>
public enum DeadLetterStatus
{
    /// <summary>
    /// The dead letter is awaiting manual intervention.
    /// </summary>
    AwaitingIntervention,

    /// <summary>
    /// The dead letter has been retried (a new execution was enqueued).
    /// </summary>
    Retried,

    /// <summary>
    /// The dead letter has been acknowledged without retry.
    /// </summary>
    Acknowledged
}
