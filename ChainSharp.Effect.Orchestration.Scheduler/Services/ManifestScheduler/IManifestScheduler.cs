using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Schedule = ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;

/// <summary>
/// Provides a type-safe API for scheduling workflows as recurring jobs.
/// </summary>
public interface IManifestScheduler
{
    /// <summary>
    /// Schedules a single workflow to run on a recurring basis.
    /// </summary>
    /// <typeparam name="TWorkflow">
    /// The workflow interface type. Must implement IEffectWorkflow&lt;TInput, TOutput&gt;
    /// for some TOutput. The scheduler resolves the workflow via WorkflowBus using the input type.
    /// </typeparam>
    /// <typeparam name="TInput">
    /// The input type for the workflow. Must implement IManifestProperties to enable
    /// serialization for scheduled job storage.
    /// </typeparam>
    /// <param name="externalId">
    /// A unique identifier for this scheduled job. Used for upsert semantics -
    /// if a manifest with this ID exists, it will be updated; otherwise, a new one is created.
    /// </param>
    /// <param name="input">The input data that will be passed to the workflow on each execution.</param>
    /// <param name="schedule">The schedule definition (interval or cron-based).</param>
    /// <param name="configure">Optional action to configure additional manifest options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created or updated manifest.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the workflow is not registered in the WorkflowRegistry.
    /// </exception>
    Task<Manifest> ScheduleAsync<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties;

    /// <summary>
    /// Schedules multiple instances of a workflow from a collection.
    /// </summary>
    /// <typeparam name="TWorkflow">
    /// The workflow interface type. Must implement IEffectWorkflow&lt;TInput, TOutput&gt;
    /// for some TOutput. The scheduler resolves the workflow via WorkflowBus using the input type.
    /// </typeparam>
    /// <typeparam name="TInput">
    /// The input type for the workflow. Must implement IManifestProperties to enable
    /// serialization for scheduled job storage.
    /// </typeparam>
    /// <typeparam name="TSource">The type of elements in the source collection.</typeparam>
    /// <param name="sources">The collection of source items to create manifests from.</param>
    /// <param name="map">
    /// A function that transforms each source item into an ExternalId and Input pair.
    /// </param>
    /// <param name="schedule">The schedule definition applied to all manifests.</param>
    /// <param name="configure">
    /// Optional action to configure additional manifest options per source item.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A read-only list of the created or updated manifests.
    /// </returns>
    /// <remarks>
    /// All manifests are created/updated in a single transaction. If any manifest
    /// fails to save, the entire batch is rolled back.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the workflow is not registered in the WorkflowRegistry.
    /// </exception>
    Task<IReadOnlyList<Manifest>> ScheduleManyAsync<TWorkflow, TInput, TSource>(
        IEnumerable<TSource> sources,
        Func<TSource, (string ExternalId, TInput Input)> map,
        Schedule schedule,
        Action<TSource, ManifestOptions>? configure = null,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties;

    /// <summary>
    /// Disables a scheduled job, preventing future executions.
    /// </summary>
    /// <param name="externalId">The external ID of the manifest to disable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// The manifest is not deleted, only disabled. Use <see cref="EnableAsync"/>
    /// to re-enable the job.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no manifest with the specified ExternalId exists.
    /// </exception>
    Task DisableAsync(string externalId, CancellationToken ct = default);

    /// <summary>
    /// Enables a previously disabled scheduled job.
    /// </summary>
    /// <param name="externalId">The external ID of the manifest to enable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no manifest with the specified ExternalId exists.
    /// </exception>
    Task EnableAsync(string externalId, CancellationToken ct = default);

    /// <summary>
    /// Triggers immediate execution of a scheduled job.
    /// </summary>
    /// <param name="externalId">The external ID of the manifest to trigger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// This creates a new execution independent of the regular schedule.
    /// The job's normal schedule continues unaffected.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no manifest with the specified ExternalId exists.
    /// </exception>
    Task TriggerAsync(string externalId, CancellationToken ct = default);
}
