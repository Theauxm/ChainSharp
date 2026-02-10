namespace ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;

/// <summary>
/// Abstraction over background task servers (Hangfire, Quartz, etc.) for enqueuing and scheduling jobs.
/// </summary>
/// <remarks>
/// This interface provides a provider-agnostic way to enqueue background tasks.
/// Implementations can wrap Hangfire, Quartz.NET, or any other background job processing library.
/// 
/// The abstraction allows ChainSharp.Effect.Scheduler to remain decoupled from specific
/// background task implementations, enabling consumers to use their preferred infrastructure.
/// 
/// Example implementations:
/// - HangfireTaskServer (in ChainSharp.Effect.Scheduler.Hangfire)
/// - QuartzTaskServer (in ChainSharp.Effect.Scheduler.Quartz)
/// </remarks>
public interface IBackgroundTaskServer
{
    /// <summary>
    /// Enqueues a job for immediate execution.
    /// </summary>
    /// <typeparam name="TJob">The type of job to enqueue (must be registered with DI)</typeparam>
    /// <param name="jobId">A unique identifier for tracking this job execution</param>
    /// <param name="payload">The serializable payload to pass to the job</param>
    /// <returns>A background task identifier for correlation/tracking</returns>
    /// <remarks>
    /// The job will be picked up by a worker as soon as one is available.
    /// The returned identifier can be stored for later correlation with the
    /// background task server's native tracking system.
    /// </remarks>
    Task<string> EnqueueAsync<TJob>(string jobId, object payload) where TJob : class;

    /// <summary>
    /// Enqueues a job for immediate execution using the job type at runtime.
    /// </summary>
    /// <param name="jobType">The type of job to enqueue</param>
    /// <param name="jobId">A unique identifier for tracking this job execution</param>
    /// <param name="payload">The serializable payload to pass to the job</param>
    /// <returns>A background task identifier for correlation/tracking</returns>
    Task<string> EnqueueAsync(Type jobType, string jobId, object payload);

    /// <summary>
    /// Schedules a job to run at a specific time.
    /// </summary>
    /// <typeparam name="TJob">The type of job to schedule</typeparam>
    /// <param name="jobId">A unique identifier for tracking this job execution</param>
    /// <param name="payload">The serializable payload to pass to the job</param>
    /// <param name="runAt">The UTC time at which to run the job</param>
    /// <returns>A background task identifier for correlation/tracking</returns>
    Task<string> ScheduleAsync<TJob>(string jobId, object payload, DateTime runAt) where TJob : class;

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <typeparam name="TJob">The type of job to schedule</typeparam>
    /// <param name="recurringJobId">A unique identifier for this recurring job definition</param>
    /// <param name="cronExpression">A cron expression defining the schedule</param>
    /// <param name="payload">The serializable payload to pass to the job on each execution</param>
    /// <remarks>
    /// If a recurring job with the same ID already exists, it will be updated with the new schedule.
    /// </remarks>
    Task AddOrUpdateRecurringAsync<TJob>(string recurringJobId, string cronExpression, object? payload = null)
        where TJob : class;

    /// <summary>
    /// Removes a recurring job definition.
    /// </summary>
    /// <param name="recurringJobId">The identifier of the recurring job to remove</param>
    Task RemoveRecurringAsync(string recurringJobId);

    /// <summary>
    /// Attempts to cancel a queued or scheduled job.
    /// </summary>
    /// <param name="backgroundTaskId">The background task identifier returned from Enqueue/Schedule</param>
    /// <returns>True if the job was successfully cancelled, false if it was already running or completed</returns>
    Task<bool> TryCancelAsync(string backgroundTaskId);
}
