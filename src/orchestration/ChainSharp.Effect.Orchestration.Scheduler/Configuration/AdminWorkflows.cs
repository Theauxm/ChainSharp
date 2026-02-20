using ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;

namespace ChainSharp.Effect.Orchestration.Scheduler.Configuration;

/// <summary>
/// Central registry of internal/administrative scheduler workflows.
/// These workflows are excluded from dashboard statistics and max-active-job counts.
/// </summary>
public static class AdminWorkflows
{
    /// <summary>
    /// The workflow types considered administrative/internal to the scheduler.
    /// </summary>
    public static readonly IReadOnlyList<Type> Types =
    [
        typeof(IManifestManagerWorkflow),
        typeof(ManifestManagerWorkflow),
        typeof(ITaskServerExecutorWorkflow),
        typeof(TaskServerExecutorWorkflow),
        typeof(IMetadataCleanupWorkflow),
        typeof(MetadataCleanupWorkflow),
        typeof(IJobDispatcherWorkflow),
        typeof(JobDispatcherWorkflow),
    ];

    /// <summary>
    /// Fully qualified type names of admin workflows.
    /// </summary>
    public static readonly IReadOnlyList<string> FullNames = Types
        .Select(t => t.FullName!)
        .ToList();

    /// <summary>
    /// Short (unqualified) type names of admin workflows, used for display filtering.
    /// </summary>
    public static readonly IReadOnlyList<string> ShortNames = Types.Select(t => t.Name).ToList();
}
