using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Samples.Scheduler.Workflows.ExtractImport.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Workflows.ExtractImport;

/// <summary>
/// Simulates an extract-import pipeline for a given table and index partition.
/// Validates the table, extracts data from the source, imports it to the destination,
/// and conditionally activates a dormant dependent quality check when anomalies are detected.
/// </summary>
public class ExtractImportWorkflow
    : EffectWorkflow<ExtractImportInput, Unit>,
        IExtractImportWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(ExtractImportInput input) =>
        Activate(input)
            .Chain<ValidateTableStep>()
            .Chain<ExtractDataStep>()
            .Chain<ImportDataStep>()
            .Chain<CheckAndActivateQualityStep>()
            .Resolve();
}
