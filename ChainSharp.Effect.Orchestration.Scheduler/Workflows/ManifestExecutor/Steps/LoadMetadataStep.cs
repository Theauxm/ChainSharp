using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestExecutor.Steps;

/// <summary>
/// Loads the Metadata record with its associated Manifest from the database.
/// </summary>
internal class LoadMetadataStep(IDataContext dataContext, ILogger<LoadMetadataStep> logger)
    : EffectStep<ExecuteManifestRequest, Metadata>
{
    public override async Task<Metadata> Run(ExecuteManifestRequest input)
    {
        logger.LogDebug(
            "Loading metadata for scheduled job execution (MetadataId: {MetadataId})",
            input.MetadataId
        );

        var metadata = await dataContext
            .Metadatas.Include(x => x.Manifest)
            .FirstOrDefaultAsync(x => x.Id == input.MetadataId);

        if (metadata is null)
            throw new WorkflowException($"Metadata with ID {input.MetadataId} not found");

        if (metadata.Manifest is null)
            throw new WorkflowException(
                $"Manifest not loaded for Metadata ID {input.MetadataId}. Ensure the Manifest is included."
            );

        logger.LogDebug(
            "Loaded metadata for workflow {WorkflowName} (MetadataId: {MetadataId})",
            metadata.Name,
            metadata.Id
        );

        return metadata;
    }
}
