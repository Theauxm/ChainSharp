using ChainSharp.Effect.Services.ServiceTrain;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Workflows.TransformLoad;

/// <summary>
/// Interface for the TransformLoad workflow.
/// Used by the WorkflowBus for workflow resolution.
/// </summary>
public interface ITransformLoadWorkflow : IServiceTrain<TransformLoadInput, Unit>;
