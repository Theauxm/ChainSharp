using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Samples.Scheduler.Workflows.TransformLoad.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Workflows.TransformLoad;

/// <summary>
/// A "Transform &amp; Load" workflow that demonstrates dependent batch scheduling.
/// Runs after ExtractImportWorkflow succeeds via .ThenMany() chaining.
/// </summary>
public class TransformLoadWorkflow
    : EffectWorkflow<TransformLoadInput, Unit>,
        ITransformLoadWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(TransformLoadInput input) =>
        Activate(input).Chain<TransformDataStep>().Chain<LoadDataStep>().Resolve();
}
