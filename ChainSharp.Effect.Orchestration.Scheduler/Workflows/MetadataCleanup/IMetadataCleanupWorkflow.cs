using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup;

/// <summary>
/// Workflow interface for cleaning up expired metadata entries.
/// </summary>
public interface IMetadataCleanupWorkflow : IEffectWorkflow<MetadataCleanupRequest, Unit>;
