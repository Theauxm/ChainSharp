using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataScience;

public interface IDataSciencePipelineWorkflow : IEffectWorkflow<DataSciencePipelineInput, Unit>;
