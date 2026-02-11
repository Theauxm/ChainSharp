using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectStep;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestExecutor.Steps;

/// <summary>
/// Updates the Manifest's LastSuccessfulRun timestamp after successful workflow execution.
/// </summary>
internal class UpdateManifestSuccessStep(
    IDataContext dataContext,
    ILogger<UpdateManifestSuccessStep> logger
) : EffectStep<Metadata, Unit>
{
    public override async Task<Unit> Run(Metadata input)
    {
        if (input.Manifest is null)
        {
            logger.LogDebug(
                "No manifest associated with Metadata {MetadataId}, skipping LastSuccessfulRun update",
                input.Id
            );
            return Unit.Default;
        }

        input.Manifest.LastSuccessfulRun = DateTime.UtcNow;

        logger.LogDebug(
            "Updated LastSuccessfulRun for Manifest {ManifestId} to {Timestamp}",
            input.Manifest.Id,
            input.Manifest.LastSuccessfulRun
        );

        await dataContext.SaveChanges(CancellationToken.None);

        return Unit.Default;
    }
}
