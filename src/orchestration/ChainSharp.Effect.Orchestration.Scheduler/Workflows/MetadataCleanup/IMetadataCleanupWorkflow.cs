using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup;

/// <summary>
/// Workflow interface for cleaning up expired metadata entries.
/// </summary>
public interface IMetadataCleanupWorkflow : IServiceTrain<MetadataCleanupRequest, Unit>;
