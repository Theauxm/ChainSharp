using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectStep;
using ChainSharp.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor.Steps;

/// <summary>
/// Loads the Metadata record from the database and resolves the workflow input.
/// </summary>
/// <remarks>
/// For manifest-based executions (Input is null on the request), the Manifest is eagerly loaded
/// and the input is resolved from the Manifest's properties.
/// For ad-hoc executions (Input is provided on the request), only the Metadata is loaded
/// and the input is taken directly from the request.
/// </remarks>
internal class LoadMetadataStep(IDataContext dataContext, ILogger<LoadMetadataStep> logger)
    : EffectStep<ExecuteManifestRequest, (Metadata, ResolvedWorkflowInput)>
{
    public override async Task<(Metadata, ResolvedWorkflowInput)> Run(ExecuteManifestRequest input)
    {
        logger.LogDebug(
            "Loading metadata for job execution (MetadataId: {MetadataId}, AdHoc: {IsAdHoc})",
            input.MetadataId,
            input.Input is not null
        );

        if (input.Input is not null)
        {
            // Ad-hoc execution: load metadata without manifest
            var metadata = await dataContext
                .Metadatas.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == input.MetadataId);

            if (metadata is null)
                throw new WorkflowException($"Metadata with ID {input.MetadataId} not found");

            logger.LogDebug(
                "Loaded ad-hoc metadata for workflow {WorkflowName} (MetadataId: {MetadataId})",
                metadata.Name,
                metadata.Id
            );

            return (metadata, new ResolvedWorkflowInput(input.Input));
        }
        else
        {
            // Manifest-based execution: load metadata with manifest (tracked so
            // UpdateManifestSuccessStep can persist LastSuccessfulRun via SaveChanges)
            var metadata = await dataContext
                .Metadatas.Include(x => x.Manifest)
                .FirstOrDefaultAsync(x => x.Id == input.MetadataId);

            if (metadata is null)
                throw new WorkflowException($"Metadata with ID {input.MetadataId} not found");

            if (metadata.Manifest is null)
                throw new WorkflowException(
                    $"Manifest not loaded for Metadata ID {input.MetadataId}. Ensure the Manifest is included."
                );

            var inputType = metadata.Manifest.PropertyType;
            var workflowInput = metadata.Manifest.GetProperties(inputType);

            logger.LogDebug(
                "Loaded manifest metadata for workflow {WorkflowName} (MetadataId: {MetadataId})",
                metadata.Name,
                metadata.Id
            );

            return (metadata, new ResolvedWorkflowInput(workflowInput));
        }
    }
}
