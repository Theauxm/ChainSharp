using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Provider.Alerting.Models;

/// <summary>
/// Defines the conditions under which a workflow should trigger an alert.
/// </summary>
/// <remarks>
/// AlertConfiguration is the result of calling Build() on AlertConfigurationBuilder.
/// It contains all the rules that determine when an alert should be sent for a workflow.
///
/// This configuration is evaluated each time a workflow fails to determine if alert
/// conditions are met.
/// </remarks>
public class AlertConfiguration
{
    /// <summary>
    /// Gets or sets the time window to look back when counting failures.
    /// </summary>
    /// <remarks>
    /// Only failures that occurred within this time window from the current time
    /// will be considered when evaluating alert conditions.
    ///
    /// For MinimumFailures == 1, this value is typically TimeSpan.Zero since
    /// historical failures are not considered.
    /// </remarks>
    public required TimeSpan TimeWindow { get; init; }

    /// <summary>
    /// Gets or sets the minimum number of failures required to trigger an alert.
    /// </summary>
    /// <remarks>
    /// If the number of qualifying failures in the TimeWindow is less than this value,
    /// no alert will be sent.
    ///
    /// When set to 1, the alert system skips the database query and sends an alert
    /// immediately for every failure (assuming other filters are satisfied).
    /// </remarks>
    public required int MinimumFailures { get; init; }

    /// <summary>
    /// Gets or sets the list of exception types to filter on.
    /// If empty, all exception types are considered.
    /// </summary>
    /// <remarks>
    /// When populated, only failures with exceptions matching one of these types
    /// will be considered for alert conditions.
    ///
    /// Multiple exception types are combined with OR logic.
    /// </remarks>
    public required List<Type> ExceptionTypes { get; init; } = new();

    /// <summary>
    /// Gets or sets the list of step name filters.
    /// Each tuple contains either an exact match string or a predicate function.
    /// If empty, all steps are considered.
    /// </summary>
    /// <remarks>
    /// When populated, only failures from steps matching one of these filters
    /// will be considered for alert conditions.
    ///
    /// Each tuple contains:
    /// - Item1 (exactMatch): A step name to match exactly, or null
    /// - Item2 (predicate): A function that takes a step name and returns true to match, or null
    ///
    /// Multiple filters are combined with OR logic.
    /// </remarks>
    public required List<(string? exactMatch, Func<string, bool>? predicate)> StepFilters {
        get;
        init;
    } = new();

    /// <summary>
    /// Gets or sets the list of custom filter predicates applied to Metadata objects.
    /// If empty, no additional filtering is applied.
    /// </summary>
    /// <remarks>
    /// These filters provide full flexibility for complex filtering logic.
    /// Each predicate receives a Metadata object and returns true to include it
    /// in the alert evaluation.
    ///
    /// Multiple predicates are combined with AND logic - all must return true
    /// for a metadata record to be included.
    ///
    /// Example:
    /// <code>
    /// .AndCustomFilter(m => m.FailureReason?.Contains("timeout") ?? false)
    /// .AndCustomFilter(m => m.StartTime.Hour >= 9 && m.StartTime.Hour <= 17)
    /// </code>
    /// </remarks>
    public required List<Func<Metadata, bool>> CustomFilters { get; init; } = new();
}
