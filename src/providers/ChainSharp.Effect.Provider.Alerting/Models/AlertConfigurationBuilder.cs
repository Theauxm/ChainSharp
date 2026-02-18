using ChainSharp.Effect.Models.Metadata;

namespace ChainSharp.Effect.Provider.Alerting.Models;

/// <summary>
/// Fluent builder for creating AlertConfiguration instances.
/// Provides a chainable API for configuring workflow alert conditions.
/// </summary>
/// <remarks>
/// AlertConfigurationBuilder uses a fluent API pattern to make alert configuration
/// intuitive and readable. The builder enforces required fields (TimeWindow and
/// MinimumFailures) through runtime validation, with compile-time enforcement
/// provided by the ChainSharp.Effect.Provider.Alerting.Analyzers package.
///
/// Example usage:
/// <code>
/// public AlertConfiguration ConfigureAlerting() =>
///     AlertConfigurationBuilder.Create()
///         .WithinTimeSpan(TimeSpan.FromHours(1))
///         .MinimumFailures(3)
///         .WhereExceptionType&lt;TimeoutException&gt;()
///         .WhereFailureStepNameEquals("DatabaseConnectionStep")
///         .AndCustomFilter(m => m.FailureReason?.Contains("timeout") ?? false)
///         .Build();
/// </code>
///
/// For simple alerting on every failure:
/// <code>
/// public AlertConfiguration ConfigureAlerting() =>
///     AlertConfigurationBuilder.Create()
///         .AlertOnEveryFailure()
///         .Build();
/// </code>
/// </remarks>
public class AlertConfigurationBuilder
{
    private TimeSpan? _timeWindow;
    private int? _minimumFailures;
    private readonly List<Type> _exceptionTypes = new();
    private readonly List<(string? exactMatch, Func<string, bool>? predicate)> _stepFilters =
        new();
    private readonly List<Func<Metadata, bool>> _customFilters = new();

    /// <summary>
    /// Creates a new AlertConfigurationBuilder instance.
    /// </summary>
    /// <returns>A new builder for configuring alert conditions</returns>
    public static AlertConfigurationBuilder Create() => new();

    /// <summary>
    /// Sets the time window to evaluate for alert conditions.
    /// REQUIRED: Must be called before Build() unless using AlertOnEveryFailure().
    /// </summary>
    /// <param name="window">The time span to look back when checking for failures</param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// The time window defines how far back to look when counting failures.
    /// For example, with TimeSpan.FromHours(1), only failures that occurred
    /// in the last hour will be considered.
    ///
    /// This is required before Build() can be called. The analyzer will
    /// enforce this at compile time.
    /// </remarks>
    public AlertConfigurationBuilder WithinTimeSpan(TimeSpan window)
    {
        _timeWindow = window;
        return this;
    }

    /// <summary>
    /// Sets the minimum number of failures required to trigger an alert.
    /// REQUIRED: Must be called before Build() unless using AlertOnEveryFailure().
    /// </summary>
    /// <param name="count">The minimum failure count (must be >= 1)</param>
    /// <returns>This builder for method chaining</returns>
    /// <exception cref="ArgumentException">Thrown if count is less than 1</exception>
    /// <remarks>
    /// This defines the threshold for alerting. If fewer than this many failures
    /// occur in the time window, no alert will be sent.
    ///
    /// When set to 1, the alert system optimizes by skipping the database query
    /// and sending an alert immediately for every failure.
    ///
    /// This is required before Build() can be called. The analyzer will
    /// enforce this at compile time.
    /// </remarks>
    public AlertConfigurationBuilder MinimumFailures(int count)
    {
        if (count < 1)
            throw new ArgumentException("Minimum failures must be >= 1", nameof(count));

        _minimumFailures = count;
        return this;
    }

    /// <summary>
    /// Convenience method that configures the alert to fire on every single failure.
    /// This satisfies both the TimeWindow and MinimumFailures requirements.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// This is equivalent to calling:
    /// <code>
    /// .WithinTimeSpan(TimeSpan.Zero)
    /// .MinimumFailures(1)
    /// </code>
    ///
    /// When MinimumFailures is 1, the alert system skips the database query
    /// and sends an alert immediately for every failure (assuming other filters
    /// are satisfied).
    /// </remarks>
    public AlertConfigurationBuilder AlertOnEveryFailure()
    {
        _timeWindow = TimeSpan.Zero;
        _minimumFailures = 1;
        return this;
    }

    /// <summary>
    /// Adds an exception type filter. Only failures with this exception type will be considered.
    /// Multiple calls add additional exception types (OR logic).
    /// </summary>
    /// <typeparam name="TException">The exception type to filter on</typeparam>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// When one or more exception types are specified, only failures caused by
    /// these exception types will be considered for alert conditions.
    ///
    /// Multiple exception types are combined with OR logic - a failure matches
    /// if its exception is ANY of the specified types.
    ///
    /// Example:
    /// <code>
    /// .WhereExceptionType&lt;TimeoutException&gt;()
    /// .WhereExceptionType&lt;DatabaseException&gt;()
    /// // Alerts on either TimeoutException OR DatabaseException
    /// </code>
    /// </remarks>
    public AlertConfigurationBuilder WhereExceptionType<TException>()
        where TException : Exception
    {
        _exceptionTypes.Add(typeof(TException));
        return this;
    }

    /// <summary>
    /// Adds a step name filter with exact equality comparison.
    /// Only failures from this specific step name will be considered.
    /// </summary>
    /// <param name="stepName">The exact step name to match</param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// This filter performs an exact string match on the FailureStep property
    /// of the Metadata record.
    ///
    /// Multiple step filters are combined with OR logic - a failure matches
    /// if its step name matches ANY of the filters.
    ///
    /// Example:
    /// <code>
    /// .WhereFailureStepNameEquals("DatabaseConnectionStep")
    /// </code>
    /// </remarks>
    public AlertConfigurationBuilder WhereFailureStepNameEquals(string stepName)
    {
        _stepFilters.Add((stepName, null));
        return this;
    }

    /// <summary>
    /// Adds a step name filter with custom predicate.
    /// Only failures where the predicate returns true will be considered.
    /// </summary>
    /// <param name="predicate">Function that takes a step name and returns true to include it</param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// This filter allows for complex step name matching logic without using
    /// wildcards or regular expressions (which can introduce unnecessary complexity).
    ///
    /// Multiple step filters are combined with OR logic - a failure matches
    /// if its step name satisfies ANY of the predicates.
    ///
    /// Example:
    /// <code>
    /// .WhereFailureStepName(step => step.StartsWith("Database"))
    /// .WhereFailureStepName(step => step.Contains("Connection"))
    /// // Alerts if step name starts with "Database" OR contains "Connection"
    /// </code>
    /// </remarks>
    public AlertConfigurationBuilder WhereFailureStepName(Func<string, bool> predicate)
    {
        _stepFilters.Add((null, predicate));
        return this;
    }

    /// <summary>
    /// Adds a custom filter predicate over the Metadata object.
    /// Provides full flexibility for complex filtering logic.
    /// </summary>
    /// <param name="filter">
    /// Function that takes a Metadata object and returns true to include it
    /// </param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// Custom filters provide maximum flexibility for alert conditions.
    /// They receive the full Metadata object and can inspect any property.
    ///
    /// Multiple custom filters are combined with AND logic - ALL filters must
    /// return true for a metadata record to be included in the alert evaluation.
    ///
    /// IMPORTANT: These filters are executed in-memory after the database query.
    /// Keep them lightweight to avoid performance issues.
    ///
    /// Example:
    /// <code>
    /// .AndCustomFilter(m => m.FailureReason?.Contains("timeout") ?? false)
    /// .AndCustomFilter(m => m.StartTime.Hour >= 9 && m.StartTime.Hour <= 17)
    /// // Both conditions must be true
    /// </code>
    /// </remarks>
    public AlertConfigurationBuilder AndCustomFilter(Func<Metadata, bool> filter)
    {
        _customFilters.Add(filter);
        return this;
    }

    /// <summary>
    /// Builds the final AlertConfiguration.
    /// REQUIRED: Must call WithinTimeSpan() and MinimumFailures() before this,
    /// OR call AlertOnEveryFailure(). The analyzer enforces this at compile time.
    /// </summary>
    /// <returns>A configured AlertConfiguration instance</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if TimeWindow or MinimumFailures has not been set
    /// </exception>
    /// <remarks>
    /// This method validates that all required fields have been set before
    /// constructing the AlertConfiguration.
    ///
    /// The ChainSharp analyzer will catch missing required fields at compile time,
    /// so this runtime validation is a fallback for scenarios where the analyzer
    /// is not active.
    /// </remarks>
    public AlertConfiguration Build()
    {
        // Runtime validation as fallback (analyzer should catch this at compile time)
        if (!_timeWindow.HasValue)
            throw new InvalidOperationException(
                "TimeWindow must be set. Call WithinTimeSpan() or AlertOnEveryFailure() before Build()."
            );

        if (!_minimumFailures.HasValue)
            throw new InvalidOperationException(
                "MinimumFailures must be set. Call MinimumFailures() or AlertOnEveryFailure() before Build()."
            );

        return new AlertConfiguration
        {
            TimeWindow = _timeWindow.Value,
            MinimumFailures = _minimumFailures.Value,
            ExceptionTypes = _exceptionTypes,
            StepFilters = _stepFilters,
            CustomFilters = _customFilters
        };
    }
}
