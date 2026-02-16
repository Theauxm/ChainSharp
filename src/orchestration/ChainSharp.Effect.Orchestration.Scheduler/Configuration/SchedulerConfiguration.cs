namespace ChainSharp.Effect.Orchestration.Scheduler.Configuration;

/// <summary>
/// Configuration options for the ChainSharp.Effect.Orchestration.Scheduler system.
/// </summary>
public class SchedulerConfiguration
{
    /// <summary>
    /// Gets the collection of pending manifests to be seeded on startup.
    /// </summary>
    /// <remarks>
    /// Pending manifests are added via the fluent configuration API
    /// (e.g., <c>.Schedule&lt;TWorkflow, TInput&gt;(...)</c>) and seeded
    /// automatically on startup by the ManifestPollingService.
    /// </remarks>
    internal List<PendingManifest> PendingManifests { get; } = [];

    /// <summary>
    /// The interval at which the ManifestManager polls for pending jobs.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The maximum number of active jobs (Pending + InProgress) allowed across all manifests.
    /// </summary>
    /// <remarks>
    /// When the total number of active jobs reaches this limit, no new jobs will be enqueued
    /// until existing jobs complete. Set to null to disable this limit (unlimited).
    /// </remarks>
    public int? MaxActiveJobs { get; set; } = 10;

    /// <summary>
    /// The default number of retry attempts before a job is dead-lettered.
    /// </summary>
    /// <remarks>
    /// This can be overridden per-manifest via ManifestScheduleProperties.
    /// </remarks>
    public int DefaultMaxRetries { get; set; } = 3;

    /// <summary>
    /// The default delay between retry attempts.
    /// </summary>
    /// <remarks>
    /// This can be combined with RetryBackoffMultiplier for exponential backoff.
    /// </remarks>
    public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Multiplier applied to retry delay on each subsequent retry (exponential backoff).
    /// </summary>
    /// <remarks>
    /// Set to 1.0 for constant retry delay. Default of 2.0 means delays of 5m, 10m, 20m, etc.
    /// </remarks>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum retry delay to prevent unbounded backoff growth.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Timeout after which a running job is considered stuck.
    /// </summary>
    /// <remarks>
    /// Jobs that have been in "InProgress" state longer than this duration
    /// may be automatically failed and potentially retried.
    /// </remarks>
    public TimeSpan DefaultJobTimeout { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Whether to automatically recover stuck jobs on scheduler startup.
    /// </summary>
    /// <remarks>
    /// If true, jobs that were "InProgress" when the system shut down will be
    /// re-evaluated on startup and potentially requeued.
    /// </remarks>
    public bool RecoverStuckJobsOnStartup { get; set; } = true;

    /// <summary>
    /// How long to retain dead letter records after resolution.
    /// </summary>
    public TimeSpan DeadLetterRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Whether to enable automatic purging of old dead letter records.
    /// </summary>
    public bool AutoPurgeDeadLetters { get; set; } = true;

    /// <summary>
    /// Configuration for automatic metadata cleanup, if enabled.
    /// </summary>
    /// <remarks>
    /// When set (via <c>.AddMetadataCleanup()</c>), a background service will
    /// periodically delete old metadata entries for the configured workflow types.
    /// Null means metadata cleanup is disabled.
    /// </remarks>
    public MetadataCleanupConfiguration? MetadataCleanup { get; set; }
}
