using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;

/// <summary>
/// Workflow interface for executing scheduled workflow jobs via the manifest system.
/// </summary>
public interface IManifestExecutorWorkflow : IEffectWorkflow<ExecuteManifestRequest, Unit>;
