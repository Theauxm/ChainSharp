using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.Reporting;

public interface IReportingPipelineWorkflow : IServiceTrain<ReportingPipelineInput, Unit>;
