using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Scheduler.Services.ManifestExecutor;

/// <summary>
/// Executes workflow jobs that have been scheduled via the manifest system.
/// </summary>
public class ManifestExecutor(
    IDataContext dataContext,
    IWorkflowBus workflowBus,
    ILogger<ManifestExecutor> logger
) : IManifestExecutor
{
    /// <inheritdoc />
    public async Task ExecuteAsync(int metadataId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing scheduled job for Metadata ID ({MetadataId})", metadataId);

        var metadata = await dataContext.Metadatas
            .Include(x => x.Manifest)
            .FirstOrDefaultAsync(x => x.Id == metadataId, cancellationToken: cancellationToken);

        metadata.AssertLoaded();

        // Validate state - must be Pending to start execution
        if (metadata.WorkflowState != WorkflowState.Pending)
        {
            logger.LogWarning(
                "Cannot execute Metadata {MetadataId} with state {State}, must be Pending.",
                metadata.Id,
                metadata.WorkflowState
            );
            throw new WorkflowException(
                $"Cannot execute a job with state {metadata.WorkflowState}, must be Pending"
            );
        }

        metadata.Manifest.AssertLoaded();

        var inputType = metadata.Manifest.PropertyType;
        var input = metadata.Manifest.GetProperties(inputType);

        logger.LogDebug(
            "Executing workflow {WorkflowName} for Metadata {MetadataId}",
            metadata.Name,
            metadata.Id
        );

        await workflowBus.RunAsync(input, metadata);

        // Update last successful run on the manifest if present
        if (metadata.Manifest != null)
        {
            metadata.Manifest.LastSuccessfulRun = DateTime.UtcNow;
            await dataContext.SaveChanges(cancellationToken);
        }
    }
}
