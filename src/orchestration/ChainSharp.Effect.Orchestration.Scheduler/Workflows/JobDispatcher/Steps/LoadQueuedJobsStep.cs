using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;

/// <summary>
/// Loads all queued work queue entries, prioritizing dependent workflows over non-dependent ones,
/// then ordering by creation time (FIFO) within each priority group.
/// </summary>
internal class LoadQueuedJobsStep(IDataContext dataContext) : EffectStep<Unit, List<WorkQueue>>
{
    public override async Task<List<WorkQueue>> Run(Unit input) =>
        await dataContext
            .WorkQueues.Include(q => q.Manifest)
            .Where(q => q.Status == WorkQueueStatus.Queued)
            .OrderByDescending(
                q => q.Manifest != null && q.Manifest.ScheduleType == ScheduleType.Dependent
            )
            .ThenBy(q => q.CreatedAt)
            .ToListAsync();
}
