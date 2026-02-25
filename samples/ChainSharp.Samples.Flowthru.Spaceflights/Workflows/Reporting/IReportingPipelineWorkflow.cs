using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.Reporting;

public interface IReportingPipelineWorkflow : IEffectWorkflow<ReportingPipelineInput, Unit>;
