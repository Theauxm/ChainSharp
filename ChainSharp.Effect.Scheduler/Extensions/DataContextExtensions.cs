using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Scheduler.Configuration;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Schedule = ChainSharp.Effect.Scheduler.Services.Scheduling.Schedule;

namespace ChainSharp.Effect.Scheduler.Extensions;

/// <summary>
/// Extension methods for <see cref="IDataContext"/> used by the scheduler.
/// </summary>
public static class DataContextExtensions
{
    /// <summary>
    /// Creates or updates a manifest with the specified configuration.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow type to schedule</typeparam>
    /// <typeparam name="TInput">The input type for the workflow</typeparam>
    /// <param name="context">The data context to use for persistence</param>
    /// <param name="externalId">The unique external identifier for the manifest</param>
    /// <param name="input">The input data for the scheduled workflow</param>
    /// <param name="schedule">The schedule configuration</param>
    /// <param name="options">Additional manifest options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created or updated manifest</returns>
    public static async Task<Manifest> UpsertManifestAsync<TWorkflow, TInput>(
        this IDataContext context,
        string externalId,
        TInput input,
        Schedule schedule,
        ManifestOptions options,
        CancellationToken ct = default
    )
        where TWorkflow : IEffectWorkflow<TInput, Unit>
        where TInput : IManifestProperties
    {
        var existing = await context.Manifests.FirstOrDefaultAsync(
            m => m.ExternalId == externalId,
            ct
        );

        if (existing != null)
        {
            // Update only scheduling-related fields, preserve runtime state
            existing.Name = typeof(TWorkflow).FullName!;
            existing.PropertyTypeName = typeof(TInput).FullName;
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
            Name = typeof(TWorkflow).FullName!,
            PropertyTypeName = typeof(TInput).FullName,
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
