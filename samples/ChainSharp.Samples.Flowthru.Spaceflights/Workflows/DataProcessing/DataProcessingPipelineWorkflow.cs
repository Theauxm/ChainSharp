using ChainSharp.Effect.Services.ServiceTrain;
using ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataProcessing.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataProcessing;

/// <summary>
/// Wraps the flowthru DataProcessing pipeline as a ChainSharp ServiceTrain.
/// Preprocesses raw company, shuttle, and review data into a model input table.
/// </summary>
public class DataProcessingPipelineWorkflow
    : ServiceTrain<DataProcessingPipelineInput, Unit>,
        IDataProcessingPipelineWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        DataProcessingPipelineInput input
    ) => Activate(input).Chain<ExecuteDataProcessingStep>().Resolve();
}
