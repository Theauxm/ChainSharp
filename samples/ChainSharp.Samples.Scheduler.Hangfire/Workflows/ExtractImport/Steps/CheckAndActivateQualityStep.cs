using ChainSharp.Effect.Orchestration.Scheduler.Services.DormantDependentContext;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.DataQualityCheck;
using ChainSharp.Step;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.ExtractImport.Steps;

/// <summary>
/// Simulates detecting data anomalies during extraction and conditionally
/// activating a dormant dependent DataQualityCheck workflow.
///
/// Demonstrates IDormantDependentContext: the quality check workflow is declared
/// in the topology (visible in the dashboard), but only fires when anomalies
/// are actually detected at runtime.
/// </summary>
public class CheckAndActivateQualityStep(
    IDormantDependentContext dormants,
    ILogger<CheckAndActivateQualityStep> logger
) : Step<ExtractImportInput, Unit>
{
    public override async Task<Unit> Run(ExtractImportInput input)
    {
        // Simulate anomaly detection — odd indexes have "anomalies"
        var anomalyCount = input.Index % 2 == 1 ? input.Index * 3 : 0;

        if (anomalyCount > 0)
        {
            logger.LogWarning(
                "[{TableName}][Index {Index}] Detected {AnomalyCount} anomalies — activating quality check",
                input.TableName,
                input.Index,
                anomalyCount
            );

            await dormants.ActivateAsync<IDataQualityCheckWorkflow, DataQualityCheckInput>(
                $"dq-check-{input.TableName.ToLower()}-{input.Index}",
                new DataQualityCheckInput
                {
                    TableName = input.TableName,
                    Index = input.Index,
                    AnomalyCount = anomalyCount,
                }
            );
        }
        else
        {
            logger.LogInformation(
                "[{TableName}][Index {Index}] No anomalies detected — quality check skipped",
                input.TableName,
                input.Index
            );
        }

        return Unit.Default;
    }
}
