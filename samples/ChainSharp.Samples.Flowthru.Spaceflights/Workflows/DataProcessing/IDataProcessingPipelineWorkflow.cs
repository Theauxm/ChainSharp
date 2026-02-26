using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataProcessing;

public interface IDataProcessingPipelineWorkflow : IServiceTrain<DataProcessingPipelineInput, Unit>;
