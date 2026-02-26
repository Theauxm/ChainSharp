using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataScience;

public interface IDataSciencePipelineWorkflow : IServiceTrain<DataSciencePipelineInput, Unit>;
