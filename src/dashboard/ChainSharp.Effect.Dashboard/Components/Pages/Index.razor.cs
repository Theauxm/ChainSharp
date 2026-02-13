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

        // Summary cards
        var todayQuery = context.Metadatas.AsNoTracking().Where(m => m.StartTime >= todayStart);

        if (hideAdmin)
            todayQuery = todayQuery.ExcludeAdmin(adminNames);

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

        // Executions over time (last 24h, grouped by hour)
        var last24h = now.AddHours(-24);
        var recentQuery = context.Metadatas.AsNoTracking().Where(m => m.StartTime >= last24h);

        if (hideAdmin)
            recentQuery = recentQuery.ExcludeAdmin(adminNames);

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
            durationsQuery = durationsQuery.ExcludeAdmin(adminNames);

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
