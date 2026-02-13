using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;

/// <summary>
/// Workflow interface for executing scheduled workflow jobs via the manifest system.
/// </summary>
public interface ITaskServerExecutorWorkflow : IEffectWorkflow<ExecuteManifestRequest, Unit>;
