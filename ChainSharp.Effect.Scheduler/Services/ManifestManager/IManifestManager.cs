using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Scheduler.Services.ManifestManager;

/// <summary>
/// Manages the polling and orchestration of manifest-based job scheduling.
/// </summary>
/// <remarks>
/// The ManifestManager is the core orchestrator of the scheduling system. It runs as a
/// recurring background job (typically once per minute) and is responsible for:
///
/// 1. Polling the Manifest table to determine which jobs need to run
/// 2. Applying scheduling rules (cron, interval, concurrency limits)
/// 3. Creating Metadata records for new job executions
/// 4. Enqueuing jobs to the background task server
/// 5. Respecting throttling and rate limiting configurations
///
/// This component acts as the "brain" of the scheduling system, making decisions about
/// what to run and when, while delegating actual execution to the ManifestExecutor.
///
/// Example flow:
/// 1. ManifestManager.ProcessPendingManifestsAsync() is called by a recurring job
/// 2. Queries Manifests that are due to run
/// 3. For each Manifest, creates a Metadata row (WorkflowState = Pending)
/// 4. Enqueues a ManifestExecutor job with the Metadata.Id
/// 5. Background worker picks up the job and runs the workflow
/// </remarks>
public interface IManifestManager
{
    /// <summary>
    /// Processes all manifests that are due for execution.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The number of jobs that were enqueued</returns>
    /// <remarks>
    /// This method is typically called by a recurring job (e.g., every minute).
    /// It evaluates each enabled manifest against its scheduling criteria and
    /// enqueues jobs as appropriate.
    /// </remarks>
    Task<int> ProcessPendingManifestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a manifest for immediate execution.
    /// </summary>
    /// <param name="manifestId">The ID of the manifest to trigger</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The created Metadata record for the execution</returns>
    /// <remarks>
    /// This method bypasses the normal scheduling rules and immediately creates
    /// a Metadata record and enqueues the job. Useful for manual re-runs or
    /// on-demand execution.
    /// </remarks>
    Task<Metadata> TriggerManifestAsync(
        int manifestId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Manually triggers a manifest with custom input properties.
    /// </summary>
    /// <param name="manifestId">The ID of the manifest to trigger</param>
    /// <param name="inputOverride">Custom input to use instead of the manifest's default properties</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The created Metadata record for the execution</returns>
    Task<Metadata> TriggerManifestAsync(
        int manifestId,
        object inputOverride,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if a manifest is currently eligible to run based on scheduling rules.
    /// </summary>
    /// <param name="manifest">The manifest to evaluate</param>
    /// <returns>True if the manifest can be scheduled, false otherwise</returns>
    /// <remarks>
    /// Evaluates criteria such as:
    /// - Is the manifest enabled?
    /// - Has the next run time passed?
    /// - Are there too many concurrent executions?
    /// - Is there already a pending/in-progress execution?
    /// </remarks>
    Task<bool> CanScheduleAsync(Manifest manifest);

    /// <summary>
    /// Bulk-enqueues multiple job executions for a single manifest.
    /// </summary>
    /// <param name="manifestId">The ID of the manifest</param>
    /// <param name="inputs">Collection of inputs, one per job to enqueue</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The created Metadata records for each execution</returns>
    /// <remarks>
    /// This is designed for scenarios like database replication where you need to
    /// quickly enqueue many jobs (e.g., one per table slice) without overloading
    /// the system. The implementation should respect throttling configurations.
    /// </remarks>
    Task<IReadOnlyList<Metadata>> BulkEnqueueAsync(
        int manifestId,
        IEnumerable<object> inputs,
        CancellationToken cancellationToken = default
    );
}
