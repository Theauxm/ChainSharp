using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Schedule = ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Orchestration.Scheduler.Configuration;

public partial class SchedulerConfigurationBuilder
{
    /// <summary>
    /// Schedules a workflow to run on a recurring basis.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <param name="externalId">A unique identifier for this scheduled job</param>
    /// <param name="input">The input data that will be passed to the workflow on each execution</param>
    /// <param name="schedule">The schedule definition (interval or cron-based)</param>
    /// <param name="configure">Optional action to configure additional manifest options</param>
    /// <param name="priority">The dispatch priority (0-31, default 0). Higher values are dispatched first. Can be overridden by the configure callback.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// The manifest is not created immediately. It is captured and will be seeded
    /// automatically on startup by the ManifestPollingService.
    /// All scheduled manifests use upsert semantics based on ExternalId.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddChainSharpEffects(options => options
    ///     .AddScheduler(scheduler => scheduler
    ///         .UseHangfire(/* ... */)
    ///         .Schedule&lt;IHelloWorldWorkflow, HelloWorldInput&gt;(
    ///             "hello-world",
    ///             new HelloWorldInput { Name = "Scheduler" },
    ///             Every.Minutes(1))
    ///     )
    /// );
    /// </code>
    /// </example>
    public SchedulerConfigurationBuilder Schedule<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Schedule schedule,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        _externalIdToGroupId[externalId] = groupId ?? externalId;

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = externalId,
                ScheduleFunc = (scheduler, ct) =>
                    scheduler.ScheduleAsync<TWorkflow, TInput>(
                        externalId,
                        input,
                        schedule,
                        configure,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    )
            }
        );

        _rootScheduledExternalId = externalId;
        _lastScheduledExternalId = externalId;

        return this;
    }

    /// <summary>
    /// Schedules a dependent workflow that runs after the previously scheduled manifest succeeds.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <param name="externalId">A unique identifier for this dependent job</param>
    /// <param name="input">The input data that will be passed to the workflow on each execution</param>
    /// <param name="configure">Optional action to configure additional manifest options</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Can be overridden by the configure callback.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// Must be called after <see cref="Schedule{TWorkflow,TInput}"/>, <see cref="Include{TWorkflow,TInput}"/>,
    /// or another <c>ThenInclude</c> call.
    /// The dependent manifest will be queued when the parent's LastSuccessfulRun is newer than its own.
    /// Supports chaining: <c>.Schedule(...).Include(...).ThenInclude(...)</c> for branched dependency chains.
    /// </remarks>
    public SchedulerConfigurationBuilder ThenInclude<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var parentExternalId =
            _lastScheduledExternalId
            ?? throw new InvalidOperationException(
                "ThenInclude() must be called after Schedule(), Include(), or another ThenInclude(). "
                    + "No parent manifest external ID is available."
            );

        _externalIdToGroupId[externalId] = groupId ?? externalId;
        _dependencyEdges.Add((parentExternalId, externalId));

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = externalId,
                ScheduleFunc = (scheduler, ct) =>
                    scheduler.ScheduleDependentAsync<TWorkflow, TInput>(
                        externalId,
                        input,
                        parentExternalId,
                        configure,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    ),
            }
        );

        _lastScheduledExternalId = externalId;

        return this;
    }

    /// <summary>
    /// Schedules a dependent workflow that runs after the root <see cref="Schedule{TWorkflow,TInput}"/> manifest succeeds.
    /// Unlike <see cref="ThenInclude{TWorkflow,TInput}"/> which chains from the most recent manifest,
    /// <c>Include</c> always branches from the root <c>Schedule</c>, enabling fan-out patterns.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow interface type</typeparam>
    /// <typeparam name="TInput">The input type for the workflow (must implement IManifestProperties)</typeparam>
    /// <param name="externalId">A unique identifier for this dependent job</param>
    /// <param name="input">The input data that will be passed to the workflow on each execution</param>
    /// <param name="configure">Optional action to configure additional manifest options</param>
    /// <param name="groupId">Optional group identifier for dashboard grouping</param>
    /// <param name="priority">The base dispatch priority (0-31, default 0). DependentPriorityBoost is added on top at dispatch time. Can be overridden by the configure callback.</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// Must be called after <see cref="Schedule{TWorkflow,TInput}"/>.
    /// Use <c>Include</c> to create multiple independent branches from a single root:
    /// <code>
    /// .Schedule&lt;A&gt;(...)           // root=A
    ///     .Include&lt;B&gt;(...)        // B depends on A (root)
    ///     .Include&lt;C&gt;(...)        // C depends on A (root)
    ///         .ThenInclude&lt;D&gt;(...)       // D depends on C (cursor)
    /// </code>
    /// Result: A → B, A → C → D
    /// </remarks>
    public SchedulerConfigurationBuilder Include<TWorkflow, TInput>(
        string externalId,
        TInput input,
        Action<ManifestOptions>? configure = null,
        string? groupId = null,
        int priority = 0
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var parentExternalId =
            _rootScheduledExternalId
            ?? throw new InvalidOperationException(
                "Include() must be called after Schedule(). "
                    + "No root manifest external ID is available."
            );

        _externalIdToGroupId[externalId] = groupId ?? externalId;
        _dependencyEdges.Add((parentExternalId, externalId));

        _configuration.PendingManifests.Add(
            new PendingManifest
            {
                ExternalId = externalId,
                ScheduleFunc = (scheduler, ct) =>
                    scheduler.ScheduleDependentAsync<TWorkflow, TInput>(
                        externalId,
                        input,
                        parentExternalId,
                        configure,
                        groupId: groupId,
                        priority: priority,
                        ct: ct
                    ),
            }
        );

        _lastScheduledExternalId = externalId;

        return this;
    }
}
