using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestExecutor;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;

/// <summary>
/// Abstraction over background task servers (Hangfire, Quartz, etc.) for enqueuing and scheduling jobs.
/// </summary>
/// <remarks>
/// This interface provides a provider-agnostic way to enqueue background tasks.
/// Implementations can wrap Hangfire, Quartz.NET, or any other background job processing library.
///
/// The abstraction allows ChainSharp.Effect.Orchestration.Scheduler to remain decoupled from specific
/// background task implementations, enabling consumers to use their preferred infrastructure.
///
/// Example implementations:
/// - HangfireTaskServer (in ChainSharp.Effect.Orchestration.Scheduler.Hangfire)
/// - QuartzTaskServer (in ChainSharp.Effect.Orchestration.Scheduler.Quartz)
///
/// Implementations should resolve <see cref="IManifestExecutorWorkflow"/> from the DI container
/// and call <see cref="IManifestExecutorWorkflow.Run"/> with an <see cref="ExecuteManifestRequest"/>.
/// </remarks>
public interface IBackgroundTaskServer
{
    /// <summary>
    /// Enqueues a job for immediate execution.
    /// </summary>
    /// <param name="metadataId">The ID of the Metadata record representing this job execution</param>
    /// <returns>A background task identifier for correlation/tracking (provider-specific)</returns>
    /// <remarks>
    /// The job will be picked up by a worker as soon as one is available.
    /// The returned identifier is provider-specific (e.g., Hangfire job ID) and can be
    /// stored for later correlation with the background task server's native tracking system.
    ///
    /// Implementations should enqueue a call to <see cref="IManifestExecutorWorkflow.Run"/>
    /// with an <see cref="ExecuteManifestRequest"/> containing the provided metadata ID.
    /// </remarks>
    Task<string> EnqueueAsync(int metadataId);

    /// <summary>
    /// Schedules a job for execution at a specified time.
    /// </summary>
    /// <param name="metadataId">The ID of the Metadata record representing this job execution</param>
    /// <param name="scheduledTime">The time at which the job should execute</param>
    /// <returns>A background task identifier for correlation/tracking (provider-specific)</returns>
    /// <remarks>
    /// The job will be picked up by a worker at or after the specified time.
    /// Useful for delayed retries or scheduled execution.
    /// </remarks>
    Task<string> ScheduleAsync(int metadataId, DateTimeOffset scheduledTime);
}
