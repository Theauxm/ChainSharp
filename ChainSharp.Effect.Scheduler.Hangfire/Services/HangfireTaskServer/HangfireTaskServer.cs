using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;
using ChainSharp.Effect.Scheduler.Workflows.ManifestManager;
using Hangfire;
using LanguageExt;

namespace ChainSharp.Effect.Scheduler.Hangfire.Services.HangfireTaskServer;

/// <summary>
/// Hangfire implementation of <see cref="IBackgroundTaskServer"/>.
/// </summary>
/// <remarks>
/// This implementation wraps Hangfire's <see cref="IBackgroundJobClient"/> and <see cref="IRecurringJobManager"/>
/// to provide background job execution for ChainSharp.Effect.Scheduler.
///
/// Jobs are enqueued to Hangfire which resolves <see cref="IManifestExecutorWorkflow"/> from the DI container
/// and calls <see cref="IManifestExecutorWorkflow.Run"/> with the metadata ID.
///
/// Example usage:
/// ```csharp
/// services.AddChainSharpScheduler();
/// services.AddHangfireTaskServer(config => config.UseSqlServerStorage(connectionString));
/// ```
/// </remarks>
public class HangfireTaskServer(
    IBackgroundJobClient backgroundJobClient,
    IRecurringJobManager recurringJobManager
) : IBackgroundTaskServer
{
    /// <inheritdoc />
    public Task<string> EnqueueAsync(int metadataId)
    {
        // Use the async overload explicitly via Expression<Func<T, Task>>
        var jobId = backgroundJobClient.Enqueue<IManifestExecutorWorkflow>(
            workflow => workflow.Run(new ExecuteManifestRequest(metadataId))
        );

        return Task.FromResult(jobId);
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync(int metadataId, DateTimeOffset scheduledTime)
    {
        // Use the async overload explicitly via Expression<Func<T, Task>>
        var jobId = backgroundJobClient.Schedule<IManifestExecutorWorkflow>(
            workflow => workflow.Run(new ExecuteManifestRequest(metadataId)),
            scheduledTime
        );

        return Task.FromResult(jobId);
    }

    /// <inheritdoc />
    public void AddOrUpdateRecurringManifestPoll(string recurringJobId, string cronExpression)
    {
        recurringJobManager.AddOrUpdate<IManifestManagerWorkflow>(
            recurringJobId,
            manager => manager.Run(Unit.Default),
            cronExpression
        );
    }
}
