using ChainSharp.Effect.Services.ServiceTrain;
using ChainSharp.Samples.Flowthru.Spaceflights.Workflows.Reporting.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.Reporting;

/// <summary>
/// Wraps the flowthru Reporting pipeline as a ChainSharp ServiceTrain.
/// Generates passenger capacity reports, charts, and PNG exports.
/// </summary>
public class ReportingPipelineWorkflow
    : ServiceTrain<ReportingPipelineInput, Unit>,
        IReportingPipelineWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        ReportingPipelineInput input
    ) => Activate(input).Chain<ExecuteReportingStep>().Resolve();
}
