using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;

namespace ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;

/// <summary>
/// In-memory implementation of <see cref="IBackgroundTaskServer"/> for testing purposes.
/// </summary>
/// <remarks>
/// This implementation executes jobs immediately and synchronously (awaitable) without
/// any external infrastructure like Hangfire or Quartz. It's useful for:
/// - Unit and integration testing
/// - Local development without background job infrastructure
/// - Simple scenarios where background processing isn't needed
///
/// Jobs are executed inline when <see cref="EnqueueAsync"/> is called, so the method
/// returns only after the workflow completes.
///
/// Example usage:
/// ```csharp
/// services.AddChainSharpEffects(options => options
///     .AddScheduler(scheduler => scheduler.UseInMemoryTaskServer())
/// );
/// ```
/// </remarks>
public class InMemoryTaskServer(IManifestExecutorWorkflow manifestExecutorWorkflow)
    : IBackgroundTaskServer
{
    private int _jobCounter;

    /// <inheritdoc />
    /// <remarks>
    /// Executes the workflow immediately and synchronously. The returned job ID is
    /// a simple incrementing counter prefixed with "inmemory-".
    /// </remarks>
    public async Task<string> EnqueueAsync(int metadataId)
    {
        var jobId = $"inmemory-{Interlocked.Increment(ref _jobCounter)}";

        await manifestExecutorWorkflow.Run(new ExecuteManifestRequest(metadataId));

        return jobId;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This implementation ignores the scheduled time and executes immediately.
    /// For testing scenarios that need delayed execution, consider using a mock instead.
    /// </remarks>
    public async Task<string> ScheduleAsync(int metadataId, DateTimeOffset scheduledTime)
    {
        // For testing, just execute immediately (ignore scheduled time)
        return await EnqueueAsync(metadataId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is a no-op in the in-memory implementation. For testing manifest polling,
    /// call <see cref="IManifestManager.ProcessPendingManifestsAsync"/> directly.
    /// </remarks>
    public void AddOrUpdateRecurringManifestPoll(string recurringJobId, string cronExpression)
    {
        // No-op for in-memory implementation
        // Tests should call IManifestManager.ProcessPendingManifestsAsync() directly
    }
}
