using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;

/// <summary>
/// Workflow interface for executing scheduled workflow jobs via the manifest system.
/// </summary>
public interface ITaskServerExecutorWorkflow : IServiceTrain<ExecuteManifestRequest, Unit>;
