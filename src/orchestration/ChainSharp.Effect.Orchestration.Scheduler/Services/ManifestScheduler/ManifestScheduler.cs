using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Orchestration.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Schedule = ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;

/// <summary>
/// Implementation of <see cref="IManifestScheduler"/> that provides type-safe manifest scheduling.
/// </summary>
public class ManifestScheduler(
    IDataContextProviderFactory dataContextFactory,
    IWorkflowRegistry workflowRegistry,
    IBackgroundTaskServer backgroundTaskServer,
    ILogger<ManifestScheduler> logger
) : IManifestScheduler
{
    /// <inheritdoc />
    public async Task<Manifest> ScheduleAsync<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var options = new ManifestOptions();
        configure?.Invoke(options);

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest = await context.UpsertManifestAsync<TWorkflow, TInput>(
            externalId,
            input,
            schedule,
            options,
            ct
        );

        await context.SaveChanges(ct);

        logger.LogInformation(
            "Scheduled workflow {Workflow} with ExternalId {ExternalId}",
            typeof(TWorkflow).Name,
            externalId
        );

        return manifest;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Manifest>> ScheduleManyAsync<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        string? prunePrefix = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        workflowRegistry.ValidateWorkflowRegistration<TInput>();

        var results = new List<Manifest>();
        var sourceList = sources.ToList();

        if (sourceList.Count == 0)
            return results;

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var transaction = await context.BeginTransaction();

        try
        {
            foreach (var source in sourceList)
            {
                var (externalId, input) = map(source);
                var options = new ManifestOptions();
                configure?.Invoke(source, options);

                var manifest = await context.UpsertManifestAsync<TWorkflow, TInput>(
                    externalId,
                    input,
                    schedule,
                    options,
                    ct
                );
                results.Add(manifest);
            }

            await context.SaveChanges(ct);

            if (prunePrefix is not null)
            {
                var keepIds = results.Select(m => m.ExternalId).ToHashSet();

                // Collect the manifest IDs to prune
                var staleManifestIds = await context
                    .Manifests.Where(
                        m => m.ExternalId.StartsWith(prunePrefix) && !keepIds.Contains(m.ExternalId)
                    )
                    .Select(m => m.Id)
                    .ToListAsync(ct);

                if (staleManifestIds.Count > 0)
                {
                    // Delete in FK-dependency order: dead_letters → metadata → manifests
                    await context
                        .DeadLetters.Where(d => staleManifestIds.Contains(d.ManifestId))
                        .ExecuteDeleteAsync(ct);

                    await context
                        .Metadatas.Where(
                            m =>
                                m.ManifestId.HasValue
                                && staleManifestIds.Contains(m.ManifestId.Value)
                        )
                        .ExecuteDeleteAsync(ct);

                    var pruned = await context
                        .Manifests.Where(m => staleManifestIds.Contains(m.Id))
                        .ExecuteDeleteAsync(ct);

                    logger.LogInformation(
                        "Pruned {Count} stale manifests with prefix '{Prefix}'",
                        pruned,
                        prunePrefix
                    );
                }
            }

            await context.CommitTransaction();

            logger.LogInformation(
                "Scheduled {Count} manifests for workflow {Workflow} in single transaction",
                results.Count,
                typeof(TWorkflow).Name
            );

            return results;
        }
        catch
        {
            await context.RollbackTransaction();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task DisableAsync(string externalId, CancellationToken ct = default)
    {
        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest =
            await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
            ?? throw new InvalidOperationException(
                $"No manifest found with ExternalId '{externalId}'"
            );

        manifest.IsEnabled = false;
        await context.SaveChanges(ct);

        logger.LogInformation("Disabled manifest {ExternalId}", externalId);
    }

    /// <inheritdoc />
    public async Task EnableAsync(string externalId, CancellationToken ct = default)
    {
        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest =
            await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
            ?? throw new InvalidOperationException(
                $"No manifest found with ExternalId '{externalId}'"
            );

        manifest.IsEnabled = true;
        await context.SaveChanges(ct);

        logger.LogInformation("Enabled manifest {ExternalId}", externalId);
    }

    /// <inheritdoc />
    public async Task TriggerAsync(string externalId, CancellationToken ct = default)
    {
        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest =
            await context.Manifests.FirstOrDefaultAsync(m => m.ExternalId == externalId, ct)
            ?? throw new InvalidOperationException(
                $"No manifest found with ExternalId '{externalId}'"
            );

        // Create a new metadata record for this triggered execution
        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = manifest.Name,
                ExternalId = Guid.NewGuid().ToString("N"),
                Input =
                    manifest.Properties != null
                        ? System.Text.Json.JsonSerializer.Deserialize<object>(manifest.Properties)
                        : null,
                ManifestId = manifest.Id
            }
        );
        context.Metadatas.Add(metadata);
        await context.SaveChanges(ct);

        // Enqueue the job for immediate execution
        await backgroundTaskServer.EnqueueAsync(metadata.Id);

        logger.LogInformation(
            "Triggered immediate execution of manifest {ExternalId} with MetadataId {MetadataId}",
            externalId,
            metadata.Id
        );
    }
}
