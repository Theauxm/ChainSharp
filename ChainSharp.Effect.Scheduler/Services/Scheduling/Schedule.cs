using ChainSharp.Effect.Enums;

namespace ChainSharp.Effect.Scheduler.Services.Scheduling;

/// <summary>
/// Represents a schedule definition for recurring job execution.
/// </summary>
/// <remarks>
/// Schedule is an immutable record that encapsulates scheduling configuration.
/// Use the static factory methods <see cref="FromInterval"/> and <see cref="FromCron"/>
/// to create instances, or use the <see cref="Every"/> and <see cref="Cron"/> helper classes
/// for more readable schedule definitions.
/// </remarks>
public record Schedule
{
    /// <summary>
    /// Gets the type of schedule (Cron or Interval).
    /// </summary>
    public ScheduleType Type { get; init; }

    /// <summary>
    /// Gets the interval for Interval-type schedules.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="Type"/> is <see cref="ScheduleType.Interval"/>.
    /// </remarks>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    /// Gets the cron expression for Cron-type schedules.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="Type"/> is <see cref="ScheduleType.Cron"/>.
    /// Uses standard 5-field cron format (minute hour day-of-month month day-of-week).
    /// </remarks>
    public string? CronExpression { get; init; }

    /// <summary>
    /// Creates a schedule from a time interval.
    /// </summary>
    /// <param name="interval">The interval between job executions</param>
    /// <returns>A new Schedule configured for interval-based execution</returns>
    /// <example>
    /// <code>
    /// var schedule = Schedule.FromInterval(TimeSpan.FromMinutes(5));
    /// </code>
    /// </example>
    public static Schedule FromInterval(TimeSpan interval) =>
        new() { Type = ScheduleType.Interval, Interval = interval };

    /// <summary>
    /// Creates a schedule from a cron expression.
    /// </summary>
    /// <param name="expression">A standard 5-field cron expression</param>
    /// <returns>A new Schedule configured for cron-based execution</returns>
    /// <example>
    /// <code>
    /// var schedule = Schedule.FromCron("0 3 * * *"); // Daily at 3am
    /// </code>
    /// </example>
    public static Schedule FromCron(string expression) =>
        new() { Type = ScheduleType.Cron, CronExpression = expression };
}
