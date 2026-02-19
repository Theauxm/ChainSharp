using ChainSharp.Step;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlertExample.Steps;

/// <summary>
/// Step that demonstrates workflow failures for the alerting system.
/// Throws different types of exceptions based on input configuration.
/// </summary>
public class DemoFailureStep(ILogger<DemoFailureStep> logger) : Step<AlertExampleInput, Unit>
{
    public override Task<Unit> Run(AlertExampleInput input)
    {
        logger.LogInformation(
            "DemoFailureStep executing - ShouldFail: {ShouldFail}, ExceptionType: {ExceptionType}",
            input.ShouldFail,
            input.ExceptionType
        );

        if (!input.ShouldFail)
        {
            logger.LogInformation("Workflow configured to succeed");
            return Task.FromResult(Unit.Default);
        }

        // Throw the configured exception type for demonstration
        logger.LogWarning(
            "Intentionally throwing {ExceptionType} to demonstrate alerting",
            input.ExceptionType
        );

        throw input.ExceptionType switch
        {
            "TimeoutException" => new TimeoutException(
                "Simulated timeout for alerting demonstration"
            ),
            "InvalidOperationException" => new InvalidOperationException(
                "Simulated invalid operation for alerting demonstration"
            ),
            "ArgumentException" => new ArgumentException(
                "Simulated argument error for alerting demonstration"
            ),
            _ => new Exception($"Simulated {input.ExceptionType} for alerting demonstration")
        };
    }
}
