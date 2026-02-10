using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Effect.Scheduler.Workflows.ManifestManager;

/// <summary>
/// Interface for the ManifestManagerWorkflow which orchestrates the manifest-based job scheduling system.
/// </summary>
public interface IManifestManagerWorkflow : IEffectWorkflow<Unit, Unit>;
