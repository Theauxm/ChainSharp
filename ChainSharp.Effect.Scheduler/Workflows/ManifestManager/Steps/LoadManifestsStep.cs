using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Scheduler.Workflows.ManifestManager.Steps;

/// <summary>
/// Loads all enabled manifests with their associated Metadatas and DeadLetters.
/// </summary>
/// <remarks>
/// This step runs first in the ManifestManagerWorkflow to perform a single database query
/// that loads all the data needed by subsequent steps. This avoids redundant database queries
/// in ReapFailedJobsStep and DetermineJobsToQueueStep.
///
/// The loaded manifests are stored in the workflow's Memory and automatically available
/// to subsequent steps.
/// </remarks>
internal class LoadManifestsStep(IDataContext dataContext) : EffectStep<Unit, List<Manifest>>
{
    public override async Task<List<Manifest>> Run(Unit input) =>
        await dataContext
            .Manifests.AsSplitQuery()
            .Where(m => m.IsEnabled)
            .Include(m => m.Metadatas)
            .Include(m => m.DeadLetters)
            .AsNoTracking()
            .ToListAsync();
}
