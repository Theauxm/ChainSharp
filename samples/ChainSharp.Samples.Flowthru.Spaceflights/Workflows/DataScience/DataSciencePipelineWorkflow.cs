using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataScience.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataScience;

/// <summary>
/// Wraps the flowthru DataScience pipeline as a ChainSharp EffectWorkflow.
/// Splits data, trains a linear regression model, and evaluates predictions.
/// </summary>
public class DataSciencePipelineWorkflow
    : EffectWorkflow<DataSciencePipelineInput, Unit>,
        IDataSciencePipelineWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        DataSciencePipelineInput input
    ) => Activate(input).Chain<ExecuteDataScienceStep>().Resolve();
}
