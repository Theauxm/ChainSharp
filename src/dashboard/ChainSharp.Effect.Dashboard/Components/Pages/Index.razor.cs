using ChainSharp.Effect.Dashboard.Services.DashboardSettings;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectRegistry;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Effect.Dashboard.Components.Pages;

public partial class Index
{
    [Inject]
    private IDataContextProviderFactory DataContextFactory { get; set; } = default!;

    [Inject]
    private IWorkflowDiscoveryService WorkflowDiscovery { get; set; } = default!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private IDashboardSettingsService DashboardSettings { get; set; } = default!;

    // Summary card values
    private int _executionsToday;
    private double _successRate;
    private int _currentlyRunning;
    private int _unresolvedDeadLetters;
    private int _activeManifests;
    private int _registeredWorkflows;

    // Chart data
    private List<ExecutionTimePoint> _executionsOverTime = [];
    private List<StateCount> _stateCounts = [];
    private List<WorkflowFailureCount> _topFailures = [];
    private List<WorkflowDuration> _avgDurations = [];

    // Tables
    private List<Metadata> _recentFailures = [];
    private List<Manifest> _activeManifestList = [];

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        await DashboardSettings.InitializeAsync();

        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        var hideAdmin = DashboardSettings.HideAdminWorkflows;
        var adminNames = DashboardSettings.AdminWorkflowNames;

        // Summary cards
        var todayQuery = context.Metadatas.AsNoTracking().Where(m => m.StartTime >= todayStart);

        if (hideAdmin)
            todayQuery = todayQuery.Where(m => !adminNames.Contains(m.Name));

        var todayMetadata = await todayQuery.ToListAsync(cancellationToken);

        _executionsToday = todayMetadata.Count;

        var completed = todayMetadata.Count(m => m.WorkflowState == WorkflowState.Completed);
        var terminal = todayMetadata.Count(
            m => m.WorkflowState is WorkflowState.Completed or WorkflowState.Failed
        );
        _successRate = terminal > 0 ? Math.Round(100.0 * completed / terminal, 1) : 0;

        var runningQuery = context
            .Metadatas.AsNoTracking()
            .Where(m => m.WorkflowState == WorkflowState.InProgress);

        if (hideAdmin)
            runningQuery = runningQuery.Where(m => !adminNames.Contains(m.Name));

        _currentlyRunning = await runningQuery.CountAsync(cancellationToken);

        _unresolvedDeadLetters = await context
            .DeadLetters.AsNoTracking()
            .CountAsync(d => d.Status == DeadLetterStatus.AwaitingIntervention, cancellationToken);

        _activeManifests = await context
            .Manifests.AsNoTracking()
            .CountAsync(m => m.IsEnabled, cancellationToken);

        _registeredWorkflows = WorkflowDiscovery.DiscoverWorkflows().Count;

        // Executions over time (last 24h, grouped by hour)
        var last24h = now.AddHours(-24);
        var recentQuery = context.Metadatas.AsNoTracking().Where(m => m.StartTime >= last24h);

        if (hideAdmin)
            recentQuery = recentQuery.Where(m => !adminNames.Contains(m.Name));

        var recentMetadata = await recentQuery
            .Select(m => new { m.StartTime, m.WorkflowState })
            .ToListAsync(cancellationToken);

        _executionsOverTime = Enumerable
            .Range(0, 24)
            .Select(i =>
            {
                var hourStart = now.AddHours(-23 + i).Date.AddHours(now.AddHours(-23 + i).Hour);
                var hourEnd = hourStart.AddHours(1);
                var inHour = recentMetadata.Where(
                    m => m.StartTime >= hourStart && m.StartTime < hourEnd
                );
                return new ExecutionTimePoint
                {
                    Hour = hourStart.ToString("HH"),
                    Completed = inHour.Count(m => m.WorkflowState == WorkflowState.Completed),
                    Failed = inHour.Count(m => m.WorkflowState == WorkflowState.Failed),
                };
            })
            .ToList();

        // State breakdown (all time today)
        _stateCounts =
        [
            new()
            {
                State = "Completed",
                Count = todayMetadata.Count(m => m.WorkflowState == WorkflowState.Completed),
            },
            new()
            {
                State = "Failed",
                Count = todayMetadata.Count(m => m.WorkflowState == WorkflowState.Failed),
            },
            new()
            {
                State = "In Progress",
                Count = todayMetadata.Count(m => m.WorkflowState == WorkflowState.InProgress),
            },
            new()
            {
                State = "Pending",
                Count = todayMetadata.Count(m => m.WorkflowState == WorkflowState.Pending),
            },
        ];

        // Top failing workflows (last 7 days)
        var last7d = now.AddDays(-7);
        var failuresQuery = context
            .Metadatas.AsNoTracking()
            .Where(m => m.WorkflowState == WorkflowState.Failed && m.StartTime >= last7d);

        if (hideAdmin)
            failuresQuery = failuresQuery.Where(m => !adminNames.Contains(m.Name));

        _topFailures = (
            await failuresQuery
                .GroupBy(m => m.Name)
                .Select(g => new WorkflowFailureCount { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(cancellationToken)
        )
            .Select(x => new WorkflowFailureCount { Name = ShortName(x.Name), Count = x.Count })
            .ToList();

        // Average duration by workflow (completed in last 7 days)
        var durationsQuery = context
            .Metadatas.AsNoTracking()
            .Where(
                m =>
                    m.WorkflowState == WorkflowState.Completed
                    && m.EndTime != null
                    && m.StartTime >= last7d
                    && m.ParentId == null
            );

        if (hideAdmin)
            durationsQuery = durationsQuery.Where(m => !adminNames.Contains(m.Name));

        var completedRecent = await durationsQuery
            .Select(
                m =>
                    new
                    {
                        m.Name,
                        m.StartTime,
                        m.EndTime,
                    }
            )
            .ToListAsync(cancellationToken);

        _avgDurations = completedRecent
            .GroupBy(m => m.Name)
            .Select(
                g =>
                    new WorkflowDuration
                    {
                        Name = ShortName(g.Key),
                        AvgMs = Math.Round(
                            g.Average(m => (m.EndTime!.Value - m.StartTime).TotalMilliseconds),
                            0
                        ),
                    }
            )
            .OrderByDescending(x => x.AvgMs)
            .Take(10)
            .ToList();

        // Recent failures
        var recentFailuresQuery = context
            .Metadatas.AsNoTracking()
            .Where(m => m.WorkflowState == WorkflowState.Failed);

        if (hideAdmin)
            recentFailuresQuery = recentFailuresQuery.Where(m => !adminNames.Contains(m.Name));

        _recentFailures = await recentFailuresQuery
            .OrderByDescending(m => m.StartTime)
            .Take(20)
            .ToListAsync(cancellationToken);

        // Active scheduled manifests
        _activeManifestList = await context
            .Manifests.AsNoTracking()
            .Where(m => m.IsEnabled && m.ScheduleType != ScheduleType.None)
            .OrderBy(m => m.Name)
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    private static string ShortName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }

    private static string FormatDuration(double ms)
    {
        if (ms < 1000)
            return $"{ms:F0}ms";
        if (ms < 60_000)
            return $"{ms / 1000:F1}s";
        return $"{ms / 60_000:F1}m";
    }

    private static string FormatSchedule(Manifest manifest) =>
        manifest.ScheduleType switch
        {
            ScheduleType.Cron => manifest.CronExpression ?? "—",
            ScheduleType.Interval
                => manifest.IntervalSeconds switch
                {
                    null => "—",
                    < 60 => $"Every {manifest.IntervalSeconds}s",
                    < 3600 => $"Every {manifest.IntervalSeconds / 60}m",
                    _ => $"Every {manifest.IntervalSeconds / 3600}h",
                },
            _ => manifest.ScheduleType.ToString(),
        };

    public class ExecutionTimePoint
    {
        public string Hour { get; init; } = "";
        public int Completed { get; init; }
        public int Failed { get; init; }
    }

    public class StateCount
    {
        public string State { get; init; } = "";
        public int Count { get; init; }
    }

    public class WorkflowFailureCount
    {
        public string Name { get; init; } = "";
        public int Count { get; init; }
    }

    public class WorkflowDuration
    {
        public string Name { get; init; } = "";
        public double AvgMs { get; init; }
    }
}
