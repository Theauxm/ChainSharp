using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;

/// <summary>
/// Interface for the JobDispatcherWorkflow which picks queued work queue entries
/// and dispatches them to the background task server.
/// </summary>
public interface IJobDispatcherWorkflow : IServiceTrain<Unit, Unit>;
