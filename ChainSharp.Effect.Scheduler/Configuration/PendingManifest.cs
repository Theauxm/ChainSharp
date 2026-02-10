using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Scheduler.Services.ManifestScheduler;

namespace ChainSharp.Effect.Scheduler.Configuration;

/// <summary>
/// Represents a manifest definition captured during startup configuration.
/// </summary>
/// <remarks>
/// PendingManifest uses a closure pattern to capture generic type information
/// at configuration time. This avoids reflection when processing manifests
/// during application startup and ensures type safety is preserved.
///
/// Pending manifests are collected during service configuration and processed
/// by <see cref="Extensions.ApplicationBuilderExtensions.UseChainSharpScheduler"/>.
/// </remarks>
internal class PendingManifest
{
    /// <summary>
    /// Gets or sets a closure that captures the generic type parameters and schedules the manifest.
    /// </summary>
    /// <remarks>
    /// This closure captures the TWorkflow and TInput generic types at configuration time,
    /// avoiding the need for reflection when the manifest is actually created during startup.
    /// The closure is invoked with an <see cref="IManifestScheduler"/> instance to perform
    /// the actual scheduling operation.
    /// </remarks>
    public required Func<
        IManifestScheduler,
        CancellationToken,
        Task<Manifest>
    > ScheduleFunc { get; init; }

    /// <summary>
    /// Gets or sets the external ID for logging and debugging purposes.
    /// </summary>
    /// <remarks>
    /// For single manifests, this is the actual ExternalId.
    /// For batch operations, this is a descriptive identifier like "sync-users... (batch)".
    /// </remarks>
    public required string ExternalId { get; init; }
}
