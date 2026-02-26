using ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher.Steps;
using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;

/// <summary>
/// Picks queued work queue entries and dispatches them as background tasks.
/// </summary>
public class JobDispatcherWorkflow : ServiceTrain<Unit, Unit>, IJobDispatcherWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
        Activate(input)
            .Chain<LoadQueuedJobsStep>()
            .Chain<LoadDispatchCapacityStep>()
            .Chain<ApplyCapacityLimitsStep>()
            .Chain<DispatchJobsStep>()
            .Resolve();
}
