using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport;

/// <summary>
/// Simulates an extract-import pipeline for a given table and index partition.
/// Validates the table, extracts data from the source, and imports it to the destination.
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
            .Resolve();
}
