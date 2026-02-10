using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Mediator.Services.WorkflowRegistry;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Scheduler.Configuration;
using ChainSharp.Effect.Scheduler.Services.BackgroundTaskServer;
using ChainSharp.Effect.Scheduler.Services.Scheduling;
using ChainSharp.Effect.Services.EffectWorkflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Scheduler.Services.ManifestScheduler;

/// <summary>
/// Implementation of <see cref="IManifestScheduler"/> that provides type-safe manifest scheduling.
/// </summary>
/// <remarks>
/// ManifestScheduler handles the complexity of creating and updating manifests while
/// maintaining type safety between workflows and their inputs. It validates workflow
/// registration at scheduling time to catch configuration errors early.
/// </remarks>
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
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        ValidateWorkflowRegistration<TInput>();

        var options = new ManifestOptions();
        configure?.Invoke(options);

        using var context =
            dataContextFactory.Create() as IDataContext
            ?? throw new InvalidOperationException("Failed to create data context");

        var manifest = await UpsertManifestAsync<TWorkflow, TInput>(
            context,
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
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        ValidateWorkflowRegistration<TInput>();

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

                var manifest = await UpsertManifestAsync<TWorkflow, TInput>(
                    context,
                    externalId,
                    input,
                    schedule,
                    options,
                    ct
                );
                results.Add(manifest);
            }

            await context.SaveChanges(ct);
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

    /// <summary>
    /// Validates that the workflow is registered in the WorkflowRegistry.
    /// </summary>
    /// <typeparam name="TInput">The input type to validate</typeparam>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no workflow is registered for the input type.
    /// </exception>
    private void ValidateWorkflowRegistration<TInput>()
    {
        var inputType = typeof(TInput);
        if (!workflowRegistry.InputTypeToWorkflow.ContainsKey(inputType))
        {
            throw new InvalidOperationException(
                $"Workflow for input type '{inputType.Name}' is not registered in the WorkflowRegistry. "
                    + $"Ensure the workflow assembly is included in AddEffectWorkflowBus()."
            );
        }
    }

    /// <summary>
    /// Creates or updates a manifest with the specified configuration.
    /// </summary>
    private async Task<Manifest> UpsertManifestAsync<TWorkflow, TInput>(
        IDataContext context,
        string externalId,
        TInput input,
        Schedule schedule,
        ManifestOptions options,
        CancellationToken ct
    )
        where TWorkflow : IEffectWorkflow
        where TInput : IManifestProperties
    {
        var existing = await context.Manifests.FirstOrDefaultAsync(
            m => m.ExternalId == externalId,
            ct
        );

        if (existing != null)
        {
            // Update only scheduling-related fields, preserve runtime state
            existing.Name = typeof(TWorkflow).AssemblyQualifiedName!;
            existing.PropertyTypeName = typeof(TInput).AssemblyQualifiedName;
            existing.SetProperties(input);
            existing.IsEnabled = options.IsEnabled;
            existing.MaxRetries = options.MaxRetries;
            existing.TimeoutSeconds = options.Timeout.HasValue
                ? (int)options.Timeout.Value.TotalSeconds
                : null;
            ApplySchedule(existing, schedule);

            return existing;
        }

        // Create new manifest
        var manifest = new Manifest
        {
            ExternalId = externalId,
            Name = typeof(TWorkflow).AssemblyQualifiedName!,
            PropertyTypeName = typeof(TInput).AssemblyQualifiedName,
            IsEnabled = options.IsEnabled,
            MaxRetries = options.MaxRetries,
            TimeoutSeconds = options.Timeout.HasValue
                ? (int)options.Timeout.Value.TotalSeconds
                : null,
        };
        manifest.SetProperties(input);
        ApplySchedule(manifest, schedule);

        context.Manifests.Add(manifest);

        return manifest;
    }

    /// <summary>
    /// Applies schedule configuration to a manifest.
    /// </summary>
    private static void ApplySchedule(Manifest manifest, Schedule schedule)
    {
        manifest.ScheduleType = schedule.Type;
        manifest.CronExpression = schedule.CronExpression;
        manifest.IntervalSeconds = schedule.Interval.HasValue
            ? (int)schedule.Interval.Value.TotalSeconds
            : null;
    }
}
