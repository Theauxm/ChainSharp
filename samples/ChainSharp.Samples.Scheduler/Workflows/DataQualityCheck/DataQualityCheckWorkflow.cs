using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Samples.Scheduler.Workflows.DataQualityCheck.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Workflows.DataQualityCheck;

/// <summary>
/// A data quality check workflow that runs only when the parent ExtractImport
/// detects anomalies. Demonstrates dormant dependent scheduling — declared in
/// the topology but only activated at runtime with context-specific input.
/// </summary>
public class DataQualityCheckWorkflow
    : EffectWorkflow<DataQualityCheckInput, Unit>,
        IDataQualityCheckWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        DataQualityCheckInput input
    ) => Activate(input).Chain<AnalyzeAnomaliesStep>().Chain<ReportResultsStep>().Resolve();
}
