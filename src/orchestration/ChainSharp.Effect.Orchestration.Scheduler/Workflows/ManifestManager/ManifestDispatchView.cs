using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.ManifestGroup;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;

/// <summary>
/// Lightweight projection of a Manifest with pre-computed aggregate flags.
/// Avoids eagerly loading unbounded child collections (Metadatas, DeadLetters, WorkQueues).
/// </summary>
internal record ManifestDispatchView
{
    public required Manifest Manifest { get; init; }
    public required ManifestGroup ManifestGroup { get; init; }
    public required int FailedCount { get; init; }
    public required bool HasAwaitingDeadLetter { get; init; }
    public required bool HasQueuedWork { get; init; }
    public required bool HasActiveExecution { get; init; }
}
