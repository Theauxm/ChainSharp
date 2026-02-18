using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Dashboard.Models;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using ChainSharp.Effect.Dashboard.Utilities;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using static ChainSharp.Effect.Dashboard.Utilities.DashboardFormatters;

namespace ChainSharp.Effect.Dashboard.Components.Pages;

public partial class Index
{
    [Inject]
    private IDataContextProviderFactory DataContextFactory { get; set; } = default!;

    [Inject]
    private IWorkflowDiscoveryService WorkflowDiscovery { get; set; } = default!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

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

        // Summary cards — single GroupBy instead of materializing all today's metadata
        var todayQuery = context.Metadatas.AsNoTracking().Where(m => m.StartTime >= todayStart);

        if (hideAdmin)
            todayQuery = todayQuery.ExcludeAdmin(adminNames);

        var todayStateCounts = await todayQuery
            .GroupBy(m => m.WorkflowState)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountForState(WorkflowState s) =>
            todayStateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0;

        _executionsToday = todayStateCounts.Sum(x => x.Count);

        var completed = CountForState(WorkflowState.Completed);
        var terminal = completed + CountForState(WorkflowState.Failed);
        _successRate = terminal > 0 ? Math.Round(100.0 * completed / terminal, 1) : 0;

        var runningQuery = context
            .Metadatas.AsNoTracking()
            .Where(m => m.WorkflowState == WorkflowState.InProgress);

        if (hideAdmin)
            runningQuery = runningQuery.ExcludeAdmin(adminNames);

        _currentlyRunning = await runningQuery.CountAsync(cancellationToken);

        _unresolvedDeadLetters = await context
            .DeadLetters.AsNoTracking()
            .CountAsync(d => d.Status == DeadLetterStatus.AwaitingIntervention, cancellationToken);

        var activeManifestsQuery = context.Manifests.AsNoTracking().Where(m => m.IsEnabled);

        if (hideAdmin)
            activeManifestsQuery = activeManifestsQuery.ExcludeAdmin(adminNames);

        _activeManifests = await activeManifestsQuery.CountAsync(cancellationToken);

        var allWorkflows = WorkflowDiscovery.DiscoverWorkflows();
        _registeredWorkflows = hideAdmin
            ? allWorkflows.Count(w => !adminNames.Contains(w.ImplementationTypeName))
            : allWorkflows.Count;

        // Executions over time (last 24h, grouped by hour) — aggregated in SQL
        var last24h = now.AddHours(-24);
        var recentQuery = context.Metadatas.AsNoTracking().Where(m => m.StartTime >= last24h);

        if (hideAdmin)
            recentQuery = recentQuery.ExcludeAdmin(adminNames);

        var hourlyStats = await recentQuery
            .GroupBy(
                m =>
                    new
                    {
                        m.StartTime.Date,
                        m.StartTime.Hour,
                        m.WorkflowState,
                    }
            )
            .Select(
                g =>
                    new
                    {
                        g.Key.Date,
                        g.Key.Hour,
                        g.Key.WorkflowState,
                        Count = g.Count(),
                    }
            )
            .ToListAsync(cancellationToken);

        _executionsOverTime = Enumerable
            .Range(0, 24)
            .Select(i =>
            {
                var hourStart = now.AddHours(-23 + i);
                var targetDate = hourStart.Date;
                var targetHour = hourStart.Hour;
                return new ExecutionTimePoint
                {
                    Hour = hourStart.ToString("HH"),
                    Completed = hourlyStats
                        .Where(
                            x =>
                                x.Date == targetDate
                                && x.Hour == targetHour
                                && x.WorkflowState == WorkflowState.Completed
                        )
                        .Sum(x => x.Count),
                    Failed = hourlyStats
                        .Where(
                            x =>
                                x.Date == targetDate
                                && x.Hour == targetHour
                                && x.WorkflowState == WorkflowState.Failed
                        )
                        .Sum(x => x.Count),
                };
            })
            .ToList();

        // State breakdown — derived from the GroupBy above, no extra query
        _stateCounts =
        [
            new() { State = "Completed", Count = CountForState(WorkflowState.Completed) },
            new() { State = "Failed", Count = CountForState(WorkflowState.Failed) },
            new() { State = "In Progress", Count = CountForState(WorkflowState.InProgress), },
            new() { State = "Pending", Count = CountForState(WorkflowState.Pending) },
        ];

        // Top failing workflows (last 7 days)
        var last7d = now.AddDays(-7);
        var failuresQuery = context
            .Metadatas.AsNoTracking()
            .Where(m => m.WorkflowState == WorkflowState.Failed && m.StartTime >= last7d);

        if (hideAdmin)
            failuresQuery = failuresQuery.ExcludeAdmin(adminNames);

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

        // Average duration by workflow (completed in last 7 days) — aggregated in SQL
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
            durationsQuery = durationsQuery.ExcludeAdmin(adminNames);

        var avgDurationData = await durationsQuery
            .GroupBy(m => m.Name)
            .Select(
                g =>
                    new
                    {
                        Name = g.Key,
                        AvgSeconds = g.Average(m => (m.EndTime!.Value - m.StartTime).TotalSeconds),
                    }
            )
            .OrderByDescending(x => x.AvgSeconds)
            .Take(10)
            .ToListAsync(cancellationToken);

        _avgDurations = avgDurationData
            .Select(
                x =>
                    new WorkflowDuration
                    {
                        Name = ShortName(x.Name),
                        AvgMs = Math.Round(x.AvgSeconds * 1000, 0),
                    }
            )
            .ToList();

        // Recent failures
        var recentFailuresQuery = context
            .Metadatas.AsNoTracking()
            .Where(m => m.WorkflowState == WorkflowState.Failed);

        if (hideAdmin)
            recentFailuresQuery = recentFailuresQuery.ExcludeAdmin(adminNames);

        _recentFailures = await recentFailuresQuery
            .OrderByDescending(m => m.StartTime)
            .Take(20)
            .ToListAsync(cancellationToken);

        // Active scheduled manifests
        var activeManifestListQuery = context
            .Manifests.AsNoTracking()
            .Where(m => m.IsEnabled && m.ScheduleType != ScheduleType.None);

        if (hideAdmin)
            activeManifestListQuery = activeManifestListQuery.ExcludeAdmin(adminNames);

        _activeManifestList = await activeManifestListQuery
            .OrderBy(m => m.Name)
            .Take(20)
            .ToListAsync(cancellationToken);
    }
}
