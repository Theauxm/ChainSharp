using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;
using Hangfire;

namespace ChainSharp.Effect.Orchestration.Scheduler.Hangfire.Services.HangfireTaskServer;

/// <summary>
/// Hangfire implementation of <see cref="IBackgroundTaskServer"/>.
/// </summary>
/// <remarks>
/// This implementation wraps Hangfire's <see cref="IBackgroundJobClient"/>
/// to provide background job execution for ChainSharp.Effect.Orchestration.Scheduler.
///
/// Jobs are enqueued to Hangfire which resolves <see cref="ITaskServerExecutorWorkflow"/> from the DI container
/// and calls <see cref="ITaskServerExecutorWorkflow.Run"/> with the metadata ID.
///
/// Example usage:
/// ```csharp
/// services.AddChainSharpEffects(options => options
///     .AddScheduler(scheduler => scheduler
///         .UseHangfire(connectionString)
///     )
/// );
/// ```
/// </remarks>
public class HangfireTaskServer(IBackgroundJobClient backgroundJobClient) : IBackgroundTaskServer
{
    /// <inheritdoc />
    public Task<string> EnqueueAsync(int metadataId)
    {
        // Use the async overload explicitly via Expression<Func<T, Task>>
        var jobId = backgroundJobClient.Enqueue<ITaskServerExecutorWorkflow>(
            workflow => workflow.Run(new ExecuteManifestRequest(metadataId))
        );

        return Task.FromResult(jobId);
    }

    /// <inheritdoc />
    public Task<string> EnqueueAsync(int metadataId, object input)
    {
        var jobId = backgroundJobClient.Enqueue<ITaskServerExecutorWorkflow>(
            workflow => workflow.Run(new ExecuteManifestRequest(metadataId, input))
        );

        return Task.FromResult(jobId);
    }
}
