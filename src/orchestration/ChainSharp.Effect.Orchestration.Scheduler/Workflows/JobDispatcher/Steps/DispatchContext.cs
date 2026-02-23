using ChainSharp.Effect.Models.WorkQueue;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;

/// <summary>
/// Carries dispatch capacity information between steps in the JobDispatcher workflow.
/// </summary>
internal record DispatchContext(
    List<WorkQueue> Entries,
    int ActiveMetadataCount,
    Dictionary<long, int> GroupActiveCounts,
    Dictionary<long, int> GroupLimits
);
