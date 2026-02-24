using ChainSharp.Effect.Provider.Alerting.Interfaces;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlertExample;

/// <summary>
/// Interface for the AlertExample workflow.
/// Demonstrates the alerting system with configurable failure behavior.
/// </summary>
public interface IAlertExampleWorkflow : IAlertingWorkflow<AlertExampleInput, Unit> { }
