using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;

/// <summary>
/// Loads all queued work queue entries ordered by creation time (FIFO).
/// </summary>
internal class LoadQueuedJobsStep(IDataContext dataContext) : EffectStep<Unit, List<WorkQueue>>
{
    public override async Task<List<WorkQueue>> Run(Unit input) =>
        await dataContext
            .WorkQueues.Where(q => q.Status == WorkQueueStatus.Queued)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();
}
