using ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;

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
public class InMemoryTaskServer(ITaskServerExecutorWorkflow taskServerExecutorWorkflow)
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

        await taskServerExecutorWorkflow.Run(new ExecuteManifestRequest(metadataId));

        return jobId;
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(int metadataId, object input)
    {
        var jobId = $"inmemory-{Interlocked.Increment(ref _jobCounter)}";

        await taskServerExecutorWorkflow.Run(new ExecuteManifestRequest(metadataId, input));

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
}
