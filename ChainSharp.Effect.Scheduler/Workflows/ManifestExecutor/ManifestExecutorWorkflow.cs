using ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor.Steps;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Effect.Scheduler.Workflows.ManifestExecutor;

/// <summary>
/// Executes workflow jobs that have been scheduled via the manifest system.
/// </summary>
/// <remarks>
/// This workflow:
/// 1. Loads the metadata and manifest from the database
/// 2. Validates the workflow state is Pending
/// 3. Executes the scheduled workflow via WorkflowBus
/// 4. Updates the manifest's LastSuccessfulRun timestamp
/// </remarks>
public class ManifestExecutorWorkflow
    : EffectWorkflow<ExecuteManifestRequest, Unit>,
        IManifestExecutorWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        ExecuteManifestRequest input
    ) =>
        Activate(input)
            .Chain<LoadMetadataStep>()
            .Chain<ValidateMetadataStateStep>()
            .Chain<ExecuteScheduledWorkflowStep>()
            .Chain<UpdateManifestSuccessStep>()
            .Resolve();
}
