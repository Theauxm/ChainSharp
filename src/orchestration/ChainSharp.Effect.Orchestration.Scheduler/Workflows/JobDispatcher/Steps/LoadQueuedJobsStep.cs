using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;

/// <summary>
/// Loads all queued work queue entries, ordered by group priority (highest first),
/// then entry priority, then creation time (FIFO).
/// Filters out entries whose ManifestGroup is disabled.
/// </summary>
internal class LoadQueuedJobsStep(IDataContext dataContext) : EffectStep<Unit, List<WorkQueue>>
{
    public override async Task<List<WorkQueue>> Run(Unit input) =>
        await dataContext
            .WorkQueues.Include(q => q.Manifest)
            .ThenInclude(m => m.ManifestGroup)
            .Where(q => q.Status == WorkQueueStatus.Queued)
            .Where(q => q.Manifest.ManifestGroup.IsEnabled)
            .OrderByDescending(q => q.Manifest.ManifestGroup.Priority)
            .ThenByDescending(q => q.Priority)
            .ThenBy(q => q.CreatedAt)
            .ToListAsync();
}
