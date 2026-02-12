using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Settings;

public partial class SchedulerSettingsPage
{
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    private SchedulerConfiguration? _config;
    private bool _available;

    // Editable TimeSpan fields
    private TimeSpanField _pollingInterval = new();
    private TimeSpanField _defaultRetryDelay = new();
    private TimeSpanField _maxRetryDelay = new();
    private TimeSpanField _defaultJobTimeout = new();
    private TimeSpanField _deadLetterRetentionPeriod = new();
    private TimeSpanField _cleanupInterval = new();
    private TimeSpanField _cleanupRetentionPeriod = new();

    private static readonly List<string> TimeUnits = ["seconds", "minutes", "hours", "days"];

    protected override void OnInitialized()
    {
        _config = ServiceProvider.GetService<SchedulerConfiguration>();
        _available = _config is not null;

        if (_available)
            LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        _pollingInterval = TimeSpanField.FromTimeSpan(_config!.PollingInterval);
        _defaultRetryDelay = TimeSpanField.FromTimeSpan(_config.DefaultRetryDelay);
        _maxRetryDelay = TimeSpanField.FromTimeSpan(_config.MaxRetryDelay);
        _defaultJobTimeout = TimeSpanField.FromTimeSpan(_config.DefaultJobTimeout);
        _deadLetterRetentionPeriod = TimeSpanField.FromTimeSpan(_config.DeadLetterRetentionPeriod);

        if (_config.MetadataCleanup is not null)
        {
            _cleanupInterval = TimeSpanField.FromTimeSpan(_config.MetadataCleanup.CleanupInterval);
            _cleanupRetentionPeriod = TimeSpanField.FromTimeSpan(
                _config.MetadataCleanup.RetentionPeriod
            );
        }
    }

    private void Save()
    {
        if (_config is null)
            return;

        _config.PollingInterval = _pollingInterval.ToTimeSpan();
        _config.MaxJobsPerCycle = _config.MaxJobsPerCycle; // already bound
        _config.DefaultMaxRetries = _config.DefaultMaxRetries; // already bound
        _config.DefaultRetryDelay = _defaultRetryDelay.ToTimeSpan();
        _config.RetryBackoffMultiplier = _config.RetryBackoffMultiplier; // already bound
        _config.MaxRetryDelay = _maxRetryDelay.ToTimeSpan();
        _config.DefaultJobTimeout = _defaultJobTimeout.ToTimeSpan();
        _config.DeadLetterRetentionPeriod = _deadLetterRetentionPeriod.ToTimeSpan();

        if (_config.MetadataCleanup is not null)
        {
            _config.MetadataCleanup.CleanupInterval = _cleanupInterval.ToTimeSpan();
            _config.MetadataCleanup.RetentionPeriod = _cleanupRetentionPeriod.ToTimeSpan();
        }

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Settings Saved",
                Detail = "Scheduler configuration updated. Most changes take effect on the next polling cycle.",
                Duration = 4000,
            }
        );
    }

    private void ResetDefaults()
    {
        if (_config is null)
            return;

        _config.PollingInterval = TimeSpan.FromSeconds(5);
        _config.MaxJobsPerCycle = 100;
        _config.DefaultMaxRetries = 3;
        _config.DefaultRetryDelay = TimeSpan.FromMinutes(5);
        _config.RetryBackoffMultiplier = 2.0;
        _config.MaxRetryDelay = TimeSpan.FromHours(1);
        _config.DefaultJobTimeout = TimeSpan.FromMinutes(20);
        _config.RecoverStuckJobsOnStartup = true;
        _config.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
        _config.AutoPurgeDeadLetters = true;

        if (_config.MetadataCleanup is not null)
        {
            _config.MetadataCleanup.CleanupInterval = TimeSpan.FromMinutes(1);
            _config.MetadataCleanup.RetentionPeriod = TimeSpan.FromHours(1);
        }

        LoadFromConfig();

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Defaults Restored",
                Detail = "All scheduler settings have been reset to their default values.",
                Duration = 4000,
            }
        );
    }

    private class TimeSpanField
    {
        public double Value { get; set; }
        public string Unit { get; set; } = "seconds";

        public TimeSpan ToTimeSpan() =>
            Unit switch
            {
                "days" => TimeSpan.FromDays(Value),
                "hours" => TimeSpan.FromHours(Value),
                "minutes" => TimeSpan.FromMinutes(Value),
                _ => TimeSpan.FromSeconds(Value),
            };

        public static TimeSpanField FromTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1 && ts.TotalDays == Math.Floor(ts.TotalDays))
                return new() { Value = ts.TotalDays, Unit = "days" };
            if (ts.TotalHours >= 1 && ts.TotalHours == Math.Floor(ts.TotalHours))
                return new() { Value = ts.TotalHours, Unit = "hours" };
            if (ts.TotalMinutes >= 1 && ts.TotalMinutes == Math.Floor(ts.TotalMinutes))
                return new() { Value = ts.TotalMinutes, Unit = "minutes" };
            return new() { Value = ts.TotalSeconds, Unit = "seconds" };
        }
    }
}
