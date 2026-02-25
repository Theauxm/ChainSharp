using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataProcessing;

public interface IDataProcessingPipelineWorkflow
    : IEffectWorkflow<DataProcessingPipelineInput, Unit>;
