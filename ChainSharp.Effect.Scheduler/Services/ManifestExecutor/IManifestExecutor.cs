using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Scheduler.Services.ManifestExecutor;

/// <summary>
/// Executes workflow jobs that have been scheduled via the manifest system.
/// </summary>
/// <remarks>
/// The ManifestExecutor is the "worker" component of the scheduling system. It is invoked
/// by the background task server when a job is dequeued and is responsible for:
///
/// 1. Loading the Metadata record by ID
/// 2. Resolving the appropriate workflow via WorkflowBus
/// 3. Executing the workflow with the stored input
/// 4. Updating the Metadata with results (success/failure)
/// 5. Handling retry logic and dead-lettering on failure
///
/// This component runs within the context of the background task server (e.g., Hangfire worker)
/// and should be designed to be idempotent where possible.
///
/// The executor does NOT make scheduling decisions - that is the ManifestManager's responsibility.
/// It simply executes what it is told to execute.
/// </remarks>
public interface IManifestExecutor
{
    /// <summary>
    /// Executes a scheduled job by its Metadata ID.
    /// </summary>
    /// <param name="metadataId">The ID of the Metadata record containing job details</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <remarks>
    /// This method is the entry point called by the background task server.
    /// It will:
    /// 1. Load the Metadata from the database
    /// 2. Set WorkflowState to InProgress
    /// 3. Resolve and execute the workflow
    /// 4. Set WorkflowState to Completed or Failed
    /// 5. On failure, increment retry count and potentially dead-letter
    /// </remarks>
    Task ExecuteAsync(int metadataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a scheduled job by its external ID.
    /// </summary>
    /// <param name="externalId">The external ID of the Metadata record</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    Task ExecuteByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-executes a previously failed job.
    /// </summary>
    /// <param name="metadataId">The ID of the failed Metadata record</param>
    /// <param name="resetRetryCount">Whether to reset the retry count (default: false)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The new Metadata record for the retry execution</returns>
    /// <remarks>
    /// Creates a new Metadata record linked to the same Manifest and executes it.
    /// The original Metadata record is preserved for audit purposes.
    /// If the job was dead-lettered, this will remove it from the dead letter queue.
    /// </remarks>
    Task<Metadata> RetryAsync(
        int metadataId,
        bool resetRetryCount = false,
        CancellationToken cancellationToken = default
    );
}
