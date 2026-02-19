using Microsoft.CodeAnalysis;

namespace ChainSharp.Effect.Provider.Alerting.Analyzers.Diagnostics;

/// <summary>
/// Diagnostic descriptors for ChainSharp alerting configuration validation.
/// </summary>
internal static class AlertingDiagnosticDescriptors
{
    /// <summary>
    /// ALERT001: AlertConfiguration requires both TimeWindow and MinimumFailures to be set.
    /// </summary>
    public static readonly DiagnosticDescriptor AlertConfigurationRequiresFields =
        new(
            id: "ALERT001",
            title: "AlertConfiguration requires TimeWindow and MinimumFailures",
            messageFormat: "AlertConfiguration.Build() called without setting required fields. "
                + "Call .WithinTimeSpan() and .MinimumFailures(), or use .AlertOnEveryFailure() before Build().",
            category: "ChainSharp.Alerting",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "AlertConfigurationBuilder requires both TimeWindow and MinimumFailures to be set before Build() can be called. "
                + "Use WithinTimeSpan() and MinimumFailures() to set these values, or use the AlertOnEveryFailure() convenience method."
        );

    /// <summary>
    /// ALERT002: UseAlertingEffect requires at least one alert sender to be registered.
    /// </summary>
    public static readonly DiagnosticDescriptor UseAlertingEffectRequiresSender =
        new(
            id: "ALERT002",
            title: "UseAlertingEffect requires at least one alert sender",
            messageFormat: "UseAlertingEffect() called without adding any alert senders. "
                + "Call options.AddAlertSender<TYourSender>() inside the configure action.",
            category: "ChainSharp.Alerting",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The alerting effect requires at least one IAlertSender implementation to be registered. "
                + "Call AddAlertSender<T>() on the AlertingOptionsBuilder inside the UseAlertingEffect configure action."
        );
}
