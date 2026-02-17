using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.TransformLoad;

/// <summary>
/// Interface for the TransformLoad workflow.
/// Used by the WorkflowBus for workflow resolution.
/// </summary>
public interface ITransformLoadWorkflow : IEffectWorkflow<TransformLoadInput, Unit>;
