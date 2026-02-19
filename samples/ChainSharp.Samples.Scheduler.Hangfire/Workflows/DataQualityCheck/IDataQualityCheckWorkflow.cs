using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.DataQualityCheck;

/// <summary>
/// Interface for the DataQualityCheck workflow.
/// Used by the WorkflowBus for workflow resolution.
/// </summary>
public interface IDataQualityCheckWorkflow : IEffectWorkflow<DataQualityCheckInput, Unit>;
