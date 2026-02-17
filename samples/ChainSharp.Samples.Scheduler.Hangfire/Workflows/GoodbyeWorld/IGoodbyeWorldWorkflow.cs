using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.GoodbyeWorld;

/// <summary>
/// Interface for the GoodbyeWorld workflow.
/// Used by the WorkflowBus for workflow resolution.
/// </summary>
public interface IGoodbyeWorldWorkflow : IEffectWorkflow<GoodbyeWorldInput, Unit>;
