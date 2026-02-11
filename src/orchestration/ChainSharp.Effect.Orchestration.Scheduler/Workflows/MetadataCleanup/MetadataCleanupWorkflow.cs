using ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup.Steps;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup;

/// <summary>
/// Deletes expired metadata entries for configured workflow types.
/// </summary>
public class MetadataCleanupWorkflow
    : EffectWorkflow<MetadataCleanupRequest, Unit>,
        IMetadataCleanupWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        MetadataCleanupRequest input
    ) => Activate(input).Chain<DeleteExpiredMetadataStep>().Resolve();
}
