using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Services.EffectRegistry;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Settings;

public partial class ServerSettingsPage
{
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    // ── Scheduler state ──
    private SchedulerConfiguration? _schedulerConfig;
    private bool _schedulerAvailable;

    private TimeSpanField _pollingInterval = new();
    private TimeSpanField _defaultRetryDelay = new();
    private TimeSpanField _maxRetryDelay = new();
    private TimeSpanField _defaultJobTimeout = new();
    private TimeSpanField _deadLetterRetentionPeriod = new();
    private TimeSpanField _cleanupInterval = new();
    private TimeSpanField _cleanupRetentionPeriod = new();

    private static readonly List<string> TimeUnits = ["seconds", "minutes", "hours", "days"];

    // Scheduler saved-state snapshots
    private bool _savedManifestManagerEnabled;
    private bool _savedJobDispatcherEnabled;
    private TimeSpan _savedPollingInterval;
    private int? _savedMaxActiveJobs;
    private int _savedDefaultMaxRetries;
    private TimeSpan _savedDefaultRetryDelay;
    private double _savedRetryBackoffMultiplier;
    private TimeSpan _savedMaxRetryDelay;
    private TimeSpan _savedDefaultJobTimeout;
    private bool _savedRecoverStuckJobsOnStartup;
    private TimeSpan _savedDeadLetterRetentionPeriod;
    private bool _savedAutoPurgeDeadLetters;
    private TimeSpan _savedCleanupInterval;
    private TimeSpan _savedCleanupRetentionPeriod;

    // ── Effects state ──
    private IEffectRegistry? _effectRegistry;
    private bool _effectsAvailable;
    private List<EffectEntry> _effects = [];
    private Dictionary<Type, bool> _savedEffectStates = new();

    // ── Dirty tracking ──
    private bool IsAdminWorkflowsDirty =>
        _schedulerAvailable
        && (
            _schedulerConfig!.ManifestManagerEnabled != _savedManifestManagerEnabled
            || _schedulerConfig.JobDispatcherEnabled != _savedJobDispatcherEnabled
        );

    private bool IsPollingQueueDirty =>
        _schedulerAvailable
        && (
            _pollingInterval.ToTimeSpan() != _savedPollingInterval
            || _schedulerConfig!.MaxActiveJobs != _savedMaxActiveJobs
        );

    private bool IsRetryDirty =>
        _schedulerAvailable
        && (
            _schedulerConfig!.DefaultMaxRetries != _savedDefaultMaxRetries
            || _defaultRetryDelay.ToTimeSpan() != _savedDefaultRetryDelay
            || _schedulerConfig.RetryBackoffMultiplier != _savedRetryBackoffMultiplier
            || _maxRetryDelay.ToTimeSpan() != _savedMaxRetryDelay
        );

    private bool IsJobSettingsDirty =>
        _schedulerAvailable
        && (
            _defaultJobTimeout.ToTimeSpan() != _savedDefaultJobTimeout
            || _schedulerConfig!.RecoverStuckJobsOnStartup != _savedRecoverStuckJobsOnStartup
        );

    private bool IsDeadLetterDirty =>
        _schedulerAvailable
        && (
            _deadLetterRetentionPeriod.ToTimeSpan() != _savedDeadLetterRetentionPeriod
            || _schedulerConfig!.AutoPurgeDeadLetters != _savedAutoPurgeDeadLetters
        );

    private bool IsMetadataCleanupDirty =>
        _schedulerAvailable
        && _schedulerConfig?.MetadataCleanup is not null
        && (
            _cleanupInterval.ToTimeSpan() != _savedCleanupInterval
            || _cleanupRetentionPeriod.ToTimeSpan() != _savedCleanupRetentionPeriod
        );

    private bool IsEffectsDirty =>
        _effectsAvailable
        && _effects.Any(
            e => e.Toggleable && e.Enabled != _savedEffectStates.GetValueOrDefault(e.FactoryType)
        );

    protected override void OnInitialized()
    {
        // Scheduler
        _schedulerConfig = ServiceProvider.GetService<SchedulerConfiguration>();
        _schedulerAvailable = _schedulerConfig is not null;

        if (_schedulerAvailable)
        {
            LoadSchedulerFromConfig();
            SnapshotSchedulerState();
        }

        // Effects
        _effectRegistry = ServiceProvider.GetService<IEffectRegistry>();
        _effectsAvailable = _effectRegistry is not null;

        if (_effectsAvailable)
        {
            LoadEffects();
            SnapshotEffectState();
        }
    }

    // ── Scheduler helpers ──

    private void LoadSchedulerFromConfig()
    {
        _pollingInterval = TimeSpanField.FromTimeSpan(_schedulerConfig!.PollingInterval);
        _defaultRetryDelay = TimeSpanField.FromTimeSpan(_schedulerConfig.DefaultRetryDelay);
        _maxRetryDelay = TimeSpanField.FromTimeSpan(_schedulerConfig.MaxRetryDelay);
        _defaultJobTimeout = TimeSpanField.FromTimeSpan(_schedulerConfig.DefaultJobTimeout);
        _deadLetterRetentionPeriod = TimeSpanField.FromTimeSpan(
            _schedulerConfig.DeadLetterRetentionPeriod
        );

        if (_schedulerConfig.MetadataCleanup is not null)
        {
            _cleanupInterval = TimeSpanField.FromTimeSpan(
                _schedulerConfig.MetadataCleanup.CleanupInterval
            );
            _cleanupRetentionPeriod = TimeSpanField.FromTimeSpan(
                _schedulerConfig.MetadataCleanup.RetentionPeriod
            );
        }
    }

    private void SaveScheduler()
    {
        if (_schedulerConfig is null)
            return;

        _schedulerConfig.PollingInterval = _pollingInterval.ToTimeSpan();
        _schedulerConfig.DefaultRetryDelay = _defaultRetryDelay.ToTimeSpan();
        _schedulerConfig.MaxRetryDelay = _maxRetryDelay.ToTimeSpan();
        _schedulerConfig.DefaultJobTimeout = _defaultJobTimeout.ToTimeSpan();
        _schedulerConfig.DeadLetterRetentionPeriod = _deadLetterRetentionPeriod.ToTimeSpan();

        if (_schedulerConfig.MetadataCleanup is not null)
        {
            _schedulerConfig.MetadataCleanup.CleanupInterval = _cleanupInterval.ToTimeSpan();
            _schedulerConfig.MetadataCleanup.RetentionPeriod = _cleanupRetentionPeriod.ToTimeSpan();
        }

        SnapshotSchedulerState();
    }

    private void ResetSchedulerDefaults()
    {
        if (_schedulerConfig is null)
            return;

        _schedulerConfig.ManifestManagerEnabled = true;
        _schedulerConfig.JobDispatcherEnabled = true;
        _schedulerConfig.PollingInterval = TimeSpan.FromSeconds(5);
        _schedulerConfig.MaxActiveJobs = 10;
        _schedulerConfig.DefaultMaxRetries = 3;
        _schedulerConfig.DefaultRetryDelay = TimeSpan.FromMinutes(5);
        _schedulerConfig.RetryBackoffMultiplier = 2.0;
        _schedulerConfig.MaxRetryDelay = TimeSpan.FromHours(1);
        _schedulerConfig.DefaultJobTimeout = TimeSpan.FromMinutes(20);
        _schedulerConfig.RecoverStuckJobsOnStartup = true;
        _schedulerConfig.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
        _schedulerConfig.AutoPurgeDeadLetters = true;

        if (_schedulerConfig.MetadataCleanup is not null)
        {
            _schedulerConfig.MetadataCleanup.CleanupInterval = TimeSpan.FromMinutes(1);
            _schedulerConfig.MetadataCleanup.RetentionPeriod = TimeSpan.FromHours(1);
        }

        LoadSchedulerFromConfig();
        SnapshotSchedulerState();
    }

    private void SnapshotSchedulerState()
    {
        _savedManifestManagerEnabled = _schedulerConfig!.ManifestManagerEnabled;
        _savedJobDispatcherEnabled = _schedulerConfig.JobDispatcherEnabled;
        _savedPollingInterval = _schedulerConfig.PollingInterval;
        _savedMaxActiveJobs = _schedulerConfig.MaxActiveJobs;
        _savedDefaultMaxRetries = _schedulerConfig.DefaultMaxRetries;
        _savedDefaultRetryDelay = _schedulerConfig.DefaultRetryDelay;
        _savedRetryBackoffMultiplier = _schedulerConfig.RetryBackoffMultiplier;
        _savedMaxRetryDelay = _schedulerConfig.MaxRetryDelay;
        _savedDefaultJobTimeout = _schedulerConfig.DefaultJobTimeout;
        _savedRecoverStuckJobsOnStartup = _schedulerConfig.RecoverStuckJobsOnStartup;
        _savedDeadLetterRetentionPeriod = _schedulerConfig.DeadLetterRetentionPeriod;
        _savedAutoPurgeDeadLetters = _schedulerConfig.AutoPurgeDeadLetters;

        if (_schedulerConfig.MetadataCleanup is not null)
        {
            _savedCleanupInterval = _schedulerConfig.MetadataCleanup.CleanupInterval;
            _savedCleanupRetentionPeriod = _schedulerConfig.MetadataCleanup.RetentionPeriod;
        }
    }

    // ── Effect helpers ──

    private void LoadEffects()
    {
        _effects = _effectRegistry!
            .GetAll()
            .Select(
                kvp =>
                    new EffectEntry
                    {
                        FactoryType = kvp.Key,
                        Name = kvp.Key.Name,
                        FullName = kvp.Key.FullName ?? kvp.Key.Name,
                        Enabled = kvp.Value,
                        Toggleable = _effectRegistry.IsToggleable(kvp.Key),
                    }
            )
            .OrderBy(e => e.Name)
            .ToList();
    }

    private void EnableAllEffects()
    {
        foreach (var entry in _effects.Where(e => e.Toggleable))
            entry.Enabled = true;
    }

    private void DisableAllEffects()
    {
        foreach (var entry in _effects.Where(e => e.Toggleable))
            entry.Enabled = false;
    }

    private void SaveEffects()
    {
        if (_effectRegistry is null)
            return;

        foreach (var entry in _effects.Where(e => e.Toggleable))
        {
            if (entry.Enabled)
                _effectRegistry.Enable(entry.FactoryType);
            else
                _effectRegistry.Disable(entry.FactoryType);
        }

        SnapshotEffectState();
    }

    private void ResetEffectDefaults()
    {
        foreach (var entry in _effects.Where(e => e.Toggleable))
            entry.Enabled = _savedEffectStates.GetValueOrDefault(entry.FactoryType);
    }

    private void SnapshotEffectState()
    {
        _savedEffectStates = _effects.ToDictionary(e => e.FactoryType, e => e.Enabled);
    }

    // ── Combined actions ──

    private void Save()
    {
        if (_schedulerAvailable)
            SaveScheduler();

        if (_effectsAvailable)
            SaveEffects();

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Settings Saved",
                Detail = "Server settings updated.",
                Duration = 4000,
            }
        );
    }

    private void ResetDefaults()
    {
        if (_schedulerAvailable)
            ResetSchedulerDefaults();

        if (_effectsAvailable)
            ResetEffectDefaults();

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Defaults Restored",
                Detail = "All server settings have been reset to their default values.",
                Duration = 4000,
            }
        );
    }

    // ── Inner types ──

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

    private class EffectEntry
    {
        public required Type FactoryType { get; init; }
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public bool Enabled { get; set; }
        public required bool Toggleable { get; init; }
    }
}
