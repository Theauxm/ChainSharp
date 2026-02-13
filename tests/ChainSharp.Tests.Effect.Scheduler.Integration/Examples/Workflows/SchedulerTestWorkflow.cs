using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

namespace ChainSharp.Tests.Effect.Scheduler.Integration.Examples.Workflows;

/// <summary>
/// A simple test workflow for scheduler integration tests.
/// Returns Unit since TaskServerExecutor uses the non-generic RunAsync which expects Unit output.
/// </summary>
public class SchedulerTestWorkflow
    : EffectWorkflow<SchedulerTestInput, Unit>,
        ISchedulerTestWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(SchedulerTestInput input) =>
        Activate(input, Unit.Default).Resolve();
}

/// <summary>
/// Input for the scheduler test workflow.
/// </summary>
public record SchedulerTestInput : IManifestProperties
{
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Interface for the scheduler test workflow.
/// </summary>
public interface ISchedulerTestWorkflow : IEffectWorkflow<SchedulerTestInput, Unit> { }

/// <summary>
/// A workflow that always fails, used for testing error handling.
/// </summary>
public class FailingSchedulerTestWorkflow
    : EffectWorkflow<FailingSchedulerTestInput, Unit>,
        IFailingSchedulerTestWorkflow
{
    protected override async Task<Either<Exception, Unit>> RunInternal(
        FailingSchedulerTestInput input
    ) => new InvalidOperationException($"Intentional failure: {input.FailureMessage}");
}

/// <summary>
/// Input for the failing scheduler test workflow.
/// </summary>
public record FailingSchedulerTestInput : IManifestProperties
{
    public string FailureMessage { get; set; } = "Test failure";
}

/// <summary>
/// Interface for the failing scheduler test workflow.
/// </summary>
public interface IFailingSchedulerTestWorkflow
    : IEffectWorkflow<FailingSchedulerTestInput, Unit> { }
