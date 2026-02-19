using ChainSharp.Effect.Provider.Alerting.Models;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlertExample.Steps;
using LanguageExt;

namespace ChainSharp.Samples.Scheduler.Hangfire.Workflows.AlertExample;

/// <summary>
/// Example workflow demonstrating the alerting system.
/// Configured to alert on every failure for demonstration purposes.
/// </summary>
public class AlertExampleWorkflow
    : EffectWorkflow<AlertExampleInput, Unit>,
        IAlertExampleWorkflow
{
    /// <summary>
    /// Configures alerting to fire on every failure.
    /// This demonstrates the simplest alert configuration.
    /// </summary>
    public AlertConfiguration ConfigureAlerting() =>
        AlertConfigurationBuilder.Create().AlertOnEveryFailure().Build();

    protected override async Task<Either<Exception, Unit>> RunInternal(
        AlertExampleInput input
    ) => Activate(input).Chain<DemoFailureStep>().Resolve();
}
