using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager.Steps;
using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;

/// <summary>
/// Orchestrates the manifest-based job scheduling system.
/// </summary>
public class ManifestManagerWorkflow : ServiceTrain<Unit, Unit>, IManifestManagerWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
        Activate(input)
            .Chain<LoadManifestsStep>()
            .Chain<ReapFailedJobsStep>()
            .Chain<DetermineJobsToQueueStep>()
            .Chain<CreateWorkQueueEntriesStep>()
            .Resolve();
}
