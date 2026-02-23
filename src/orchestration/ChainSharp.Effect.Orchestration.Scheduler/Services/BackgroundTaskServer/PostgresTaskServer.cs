using System.Text.Json;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Models.BackgroundJob;
using ChainSharp.Effect.Models.BackgroundJob.DTOs;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;
using ChainSharp.Effect.Utils;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;

/// <summary>
/// Built-in PostgreSQL implementation of <see cref="IBackgroundTaskServer"/>.
/// </summary>
/// <remarks>
/// Enqueues jobs by inserting into the <c>chain_sharp.background_job</c> table.
/// Jobs are picked up by <see cref="PostgresWorkerService.PostgresWorkerService"/>
/// which polls the table using <c>FOR UPDATE SKIP LOCKED</c> for atomic dequeue.
/// </remarks>
public class PostgresTaskServer(IDataContext dataContext) : IBackgroundTaskServer
{
    /// <inheritdoc />
    public async Task<string> EnqueueAsync(long metadataId)
    {
        var job = BackgroundJob.Create(new CreateBackgroundJob { MetadataId = metadataId });

        await dataContext.Track(job);
        await dataContext.SaveChanges(CancellationToken.None);

        return job.Id.ToString();
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync(long metadataId, object input)
    {
        var inputJson = JsonSerializer.Serialize(
            input,
            input.GetType(),
            ChainSharpJsonSerializationOptions.ManifestProperties
        );

        var job = BackgroundJob.Create(
            new CreateBackgroundJob
            {
                MetadataId = metadataId,
                Input = inputJson,
                InputType = input.GetType().FullName,
            }
        );

        await dataContext.Track(job);
        await dataContext.SaveChanges(CancellationToken.None);

        return job.Id.ToString();
    }
}
