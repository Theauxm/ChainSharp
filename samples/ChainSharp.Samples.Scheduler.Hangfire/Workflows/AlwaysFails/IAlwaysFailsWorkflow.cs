using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlwaysFails;

/// <summary>
/// Interface for the AlwaysFails workflow.
/// This workflow always throws an exception, which causes it to dead-letter
/// after exhausting its retry budget—useful for testing dead letter resolution.
/// </summary>
public interface IAlwaysFailsWorkflow : IEffectWorkflow<AlwaysFailsInput, Unit>;
