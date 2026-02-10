namespace ChainSharp.Effect.Scheduler.Configuration;

/// <summary>
/// Configuration options for the ChainSharp.Effect.Scheduler system.
/// </summary>
public class SchedulerConfiguration
{
    /// <summary>
    /// Gets the collection of pending manifests to be seeded on startup.
    /// </summary>
    /// <remarks>
    /// Pending manifests are added via the fluent configuration API
    /// (e.g., <c>.Schedule&lt;TWorkflow, TInput&gt;(...)</c>) and processed
    /// by <see cref="Extensions.ApplicationBuilderExtensions.UseChainSharpScheduler"/>.
    /// </remarks>
    internal List<PendingManifest> PendingManifests { get; } = [];

    /// <summary>
    /// The interval at which the ManifestManager polls for pending jobs.
    /// </summary>
    /// <remarks>
    /// Default is 60 seconds. Lower values mean faster job pickup but more database load.
    /// </remarks>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The maximum number of jobs that can be enqueued in a single polling cycle.
    /// </summary>
    /// <remarks>
    /// This prevents the scheduler from overwhelming the background task server
    /// with too many jobs at once. Jobs exceeding this limit will be picked up
    /// in the next polling cycle.
    /// </remarks>
    public int MaxJobsPerCycle { get; set; } = 100;

    /// <summary>
    /// The maximum number of concurrent executions allowed for a single manifest.
    /// </summary>
    /// <remarks>
    /// Set to 0 for unlimited. This can be overridden per-manifest.
    /// </remarks>
    public int DefaultMaxConcurrentExecutions { get; set; } = 1;

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
    public TimeSpan DefaultJobTimeout { get; set; } = TimeSpan.FromHours(1);

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
    /// The cron expression for the ManifestManager's recurring job.
    /// </summary>
    /// <remarks>
    /// Default is "* * * * *" (every minute). This controls how often
    /// the scheduler checks for pending manifests to execute.
    /// </remarks>
    public string ManagerCronExpression { get; set; } = "* * * * *";
}
