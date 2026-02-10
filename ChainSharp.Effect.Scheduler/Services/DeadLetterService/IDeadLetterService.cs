using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Scheduler.Services.DeadLetterService;

/// <summary>
/// Manages the dead letter queue for jobs that have exceeded their retry limits.
/// </summary>
/// <remarks>
/// The DeadLetterService handles jobs that have failed repeatedly and require manual
/// intervention. It provides:
///
/// 1. Dead-lettering logic (when to move a job to the dead letter queue)
/// 2. Querying of dead-lettered jobs
/// 3. Manual intervention workflows (retry, acknowledge, purge)
/// 4. Alerting integration points
///
/// A job is dead-lettered when:
/// - It has exceeded its maximum retry count
/// - It has failed with a non-retryable exception type
/// - It has been manually dead-lettered by an operator
///
/// Dead-lettered jobs remain in the system for audit and debugging purposes
/// until they are either retried successfully or explicitly purged.
/// </remarks>
public interface IDeadLetterService
{
    /// <summary>
    /// Moves a failed job execution to the dead letter queue.
    /// </summary>
    /// <param name="manifestId">The ID of the Manifest (job definition) to dead-letter</param>
    /// <param name="reason">The reason for dead-lettering</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The created DeadLetter record</returns>
    Task<DeadLetter> DeadLetterAsync(
        int manifestId,
        string reason,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Determines if a failed job should be dead-lettered based on retry policy.
    /// </summary>
    /// <param name="metadata">The failed Metadata record</param>
    /// <returns>True if the job should be dead-lettered, false if it should be retried</returns>
    Task<bool> ShouldDeadLetterAsync(Metadata metadata);

    /// <summary>
    /// Retrieves all dead-lettered jobs, optionally filtered.
    /// </summary>
    /// <param name="manifestId">Optional filter by manifest</param>
    /// <param name="fromDate">Optional filter by date range start</param>
    /// <param name="toDate">Optional filter by date range end</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Collection of dead letter records</returns>
    Task<IReadOnlyList<DeadLetter>> GetDeadLettersAsync(
        int? manifestId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retries a dead-lettered job.
    /// </summary>
    /// <param name="deadLetterId">The ID of the DeadLetter record</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The new Metadata record for the retry execution</returns>
    /// <remarks>
    /// This creates a new Metadata record and enqueues the job for execution.
    /// The DeadLetter record is marked as resolved but retained for audit.
    /// </remarks>
    Task<Metadata> RetryDeadLetterAsync(
        int deadLetterId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Acknowledges a dead-lettered job without retrying.
    /// </summary>
    /// <param name="deadLetterId">The ID of the DeadLetter record</param>
    /// <param name="acknowledgementNote">A note explaining why the job is being acknowledged</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <remarks>
    /// Use this when a dead-lettered job has been handled out-of-band or is
    /// no longer relevant. The job will not be retried but remains in the
    /// dead letter history for audit purposes.
    /// </remarks>
    Task AcknowledgeAsync(
        int deadLetterId,
        string acknowledgementNote,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Permanently removes old dead letter records.
    /// </summary>
    /// <param name="olderThan">Purge records older than this date</param>
    /// <param name="onlyAcknowledged">If true, only purge acknowledged records</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The number of records purged</returns>
    Task<int> PurgeAsync(
        DateTime olderThan,
        bool onlyAcknowledged = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets statistics about the dead letter queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Summary statistics</returns>
    Task<DeadLetterStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary statistics for the dead letter queue.
/// </summary>
public record DeadLetterStatistics
{
    /// <summary>Total number of unresolved dead letters.</summary>
    public int TotalUnresolved { get; init; }

    /// <summary>Number of dead letters awaiting manual intervention.</summary>
    public int AwaitingIntervention { get; init; }

    /// <summary>Number of dead letters that have been acknowledged.</summary>
    public int Acknowledged { get; init; }

    /// <summary>Number of dead letters that were successfully retried.</summary>
    public int RetriedSuccessfully { get; init; }

    /// <summary>Breakdown by manifest.</summary>
    public IReadOnlyDictionary<int, int> CountByManifest { get; init; } =
        new Dictionary<int, int>();

    /// <summary>Most recent dead letter timestamp.</summary>
    public DateTime? MostRecentDeadLetter { get; init; }
}
