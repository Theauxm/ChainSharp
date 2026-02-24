using ChainSharp.Effect.Models.Manifest;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlertExample;

/// <summary>
/// Input for the AlertExample workflow.
/// Demonstrates the alerting system by intentionally failing when ShouldFail is true.
/// </summary>
public record AlertExampleInput : IManifestProperties
{
    /// <summary>
    /// When true, the workflow will throw an exception to demonstrate alerting.
    /// </summary>
    public bool ShouldFail { get; init; } = true;

    /// <summary>
    /// The type of exception to throw (for testing different exception filters).
    /// </summary>
    public string ExceptionType { get; init; } = "TimeoutException";
}
