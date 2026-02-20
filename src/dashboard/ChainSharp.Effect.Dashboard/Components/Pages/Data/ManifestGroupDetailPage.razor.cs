using System.Linq.Dynamic.Core;
using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Dashboard.Models;
using ChainSharp.Effect.Dashboard.Utilities;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.ManifestGroup;
using ChainSharp.Effect.Models.Metadata;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;
using static ChainSharp.Effect.Dashboard.Utilities.DashboardFormatters;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Data;

public partial class ManifestGroupDetailPage
{
    [Inject]
    private IDataContextProviderFactory DataContextFactory { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public int ManifestGroupId { get; set; }

    protected override object? GetRouteKey() => ManifestGroupId;

    private ManifestGroup? _group;
    private DagLayout? _dagLayout;

    // ── Summary counts (efficient DB aggregates) ──
    private int _manifestCount;
    private int _completedCount;
    private int _failedCount;
    private int _inProgressCount;

    // ── Grid references for server-side reload ──
    private ChainSharpDataGrid<Manifest>? _manifestsGrid;
    private ChainSharpDataGrid<Metadata>? _executionsGrid;

    // ── Settings dirty tracking ──
    private int? _savedMaxActiveJobs;
    private int _savedPriority;
    private bool _savedIsEnabled;

    private bool IsSettingsDirty =>
        _group is not null
        && (
            _group.MaxActiveJobs != _savedMaxActiveJobs
            || _group.Priority != _savedPriority
            || _group.IsEnabled != _savedIsEnabled
        );

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        var freshGroup = await context.ManifestGroups.FirstOrDefaultAsync(
            g => g.Id == ManifestGroupId,
            cancellationToken
        );

        if (freshGroup is null)
        {
            _group = null;
            _manifestCount = 0;
            _completedCount = 0;
            _failedCount = 0;
            _inProgressCount = 0;
            _dagLayout = null;
            return;
        }

        // Don't overwrite the user's unsaved edits during poll ticks
        if (!IsSettingsDirty)
        {
            _group = freshGroup;
            SnapshotSettings();
        }

        // Efficient COUNTs for summary cards
        _manifestCount = await context
            .Manifests.AsNoTracking()
            .CountAsync(m => m.ManifestGroupId == ManifestGroupId, cancellationToken);

        // Subquery for scoping execution counts to this group's manifests.
        // No AsNoTracking — this is composed into outer queries, never materialized.
        var manifestIdsSubquery = context
            .Manifests.Where(m => m.ManifestGroupId == ManifestGroupId)
            .Select(m => m.Id);

        var executionsBase = context
            .Metadatas.AsNoTracking()
            .Where(m => m.ManifestId.HasValue && manifestIdsSubquery.Contains(m.ManifestId.Value));

        _completedCount = await executionsBase.CountAsync(
            m => m.WorkflowState == WorkflowState.Completed,
            cancellationToken
        );
        _failedCount = await executionsBase.CountAsync(
            m => m.WorkflowState == WorkflowState.Failed,
            cancellationToken
        );
        _inProgressCount = await executionsBase.CountAsync(
            m => m.WorkflowState == WorkflowState.InProgress,
            cancellationToken
        );

        // Build 1-hop neighborhood dependency graph
        await LoadDependencyGraph(context, cancellationToken);

        // Tell grids to reload their current page from the server
        if (_manifestsGrid is not null)
            await _manifestsGrid.ReloadAsync();
        if (_executionsGrid is not null)
            await _executionsGrid.ReloadAsync();
    }

    // ── Server-side grid callbacks ──

    private async Task<ServerDataResult<Manifest>> LoadManifestPageAsync(
        LoadDataArgs args,
        CancellationToken cancellationToken
    )
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Manifest> query = context
            .Manifests.AsNoTracking()
            .Where(m => m.ManifestGroupId == ManifestGroupId);

        if (!string.IsNullOrEmpty(args.Filter))
            query = query.Where(args.Filter);

        if (!string.IsNullOrEmpty(args.OrderBy))
            query = query.OrderBy(args.OrderBy);
        else
            query = query.OrderByDescending(m => m.Id);

        var count = await query.CountAsync(cancellationToken);

        if (args.Skip.HasValue)
            query = query.Skip(args.Skip.Value);
        if (args.Top.HasValue)
            query = query.Take(args.Top.Value);

        var items = await query.ToListAsync(cancellationToken);
        return new ServerDataResult<Manifest>(items, count);
    }

    private async Task<ServerDataResult<Metadata>> LoadExecutionPageAsync(
        LoadDataArgs args,
        CancellationToken cancellationToken
    )
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        // Subquery — generates SQL subselect, not a materialized IN list.
        // No AsNoTracking — this is composed into the outer query, never materialized.
        var manifestIdsSubquery = context
            .Manifests.Where(m => m.ManifestGroupId == ManifestGroupId)
            .Select(m => m.Id);

        IQueryable<Metadata> query = context
            .Metadatas.AsNoTracking()
            .Where(m => m.ManifestId.HasValue && manifestIdsSubquery.Contains(m.ManifestId.Value));

        if (!string.IsNullOrEmpty(args.Filter))
            query = query.Where(args.Filter);

        if (!string.IsNullOrEmpty(args.OrderBy))
            query = query.OrderBy(args.OrderBy);
        else
            query = query.OrderByDescending(m => m.StartTime);

        var count = await query.CountAsync(cancellationToken);

        if (args.Skip.HasValue)
            query = query.Skip(args.Skip.Value);
        if (args.Top.HasValue)
            query = query.Take(args.Top.Value);

        var items = await query.ToListAsync(cancellationToken);
        return new ServerDataResult<Metadata>(items, count);
    }

    // ── Dependency graph ──

    private async Task LoadDependencyGraph(
        Effect.Data.Services.DataContext.IDataContext context,
        CancellationToken cancellationToken
    )
    {
        var currentManifestIdsQuery = context
            .Manifests.Where(m => m.ManifestGroupId == ManifestGroupId)
            .Select(m => m.Id);

        if (!await currentManifestIdsQuery.AnyAsync(cancellationToken))
        {
            _dagLayout = null;
            return;
        }

        // Upstream: groups containing manifests that our manifests depend on
        var upstreamGroupIds = await context
            .Manifests.AsNoTracking()
            .Where(m => m.ManifestGroupId == ManifestGroupId && m.DependsOnManifestId != null)
            .Join(
                context.Manifests.AsNoTracking(),
                dependent => dependent.DependsOnManifestId,
                parent => (int?)parent.Id,
                (dependent, parent) => parent.ManifestGroupId
            )
            .Where(parentGroupId => parentGroupId != ManifestGroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Downstream: groups containing manifests that depend on our manifests
        var downstreamGroupIds = await context
            .Manifests.AsNoTracking()
            .Where(
                m =>
                    m.DependsOnManifestId != null
                    && currentManifestIdsQuery.Contains(m.DependsOnManifestId.Value)
                    && m.ManifestGroupId != ManifestGroupId
            )
            .Select(m => m.ManifestGroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var neighborGroupIds = upstreamGroupIds.Union(downstreamGroupIds).ToHashSet();

        if (neighborGroupIds.Count == 0)
        {
            _dagLayout = null;
            return;
        }

        var allRelevantGroupIds = neighborGroupIds.Append(ManifestGroupId).ToList();

        var neighborGroups = await context
            .ManifestGroups.AsNoTracking()
            .Where(g => allRelevantGroupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        var dagNodes = neighborGroups
            .Select(
                g =>
                    new DagNode
                    {
                        Id = g.Id,
                        Label = g.Name,
                        IsHighlighted = g.Id == ManifestGroupId,
                    }
            )
            .ToList();

        // Edges between all relevant groups
        var crossGroupEdges = await context
            .Manifests.AsNoTracking()
            .Where(
                m =>
                    m.DependsOnManifestId != null && allRelevantGroupIds.Contains(m.ManifestGroupId)
            )
            .Join(
                context.Manifests.AsNoTracking(),
                dependent => dependent.DependsOnManifestId,
                parent => (int?)parent.Id,
                (dependent, parent) =>
                    new
                    {
                        ParentGroupId = parent.ManifestGroupId,
                        DependentGroupId = dependent.ManifestGroupId,
                    }
            )
            .Where(
                e =>
                    e.ParentGroupId != e.DependentGroupId
                    && allRelevantGroupIds.Contains(e.ParentGroupId)
            )
            .Distinct()
            .ToListAsync(cancellationToken);

        var dagEdges = crossGroupEdges
            .Select(e => new DagEdge { FromId = e.ParentGroupId, ToId = e.DependentGroupId })
            .ToList();

        _dagLayout = DagLayoutEngine.ComputeLayout(dagNodes, dagEdges);
    }

    // ── Settings ──

    private void SnapshotSettings()
    {
        if (_group is null)
            return;

        _savedMaxActiveJobs = _group.MaxActiveJobs;
        _savedPriority = _group.Priority;
        _savedIsEnabled = _group.IsEnabled;
    }

    private async Task SaveSettings()
    {
        if (_group is null)
            return;

        try
        {
            using var context = await DataContextFactory.CreateDbContextAsync(
                CancellationToken.None
            );

            var entity = await context.ManifestGroups.FindAsync(_group.Id);
            if (entity is null)
                return;

            entity.MaxActiveJobs = _group.MaxActiveJobs;
            entity.Priority = _group.Priority;
            entity.IsEnabled = _group.IsEnabled;
            entity.UpdatedAt = DateTime.UtcNow;

            await context.SaveChanges(CancellationToken.None);

            SnapshotSettings();

            NotificationService.Notify(
                new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Settings Saved",
                    Detail = $"Group \"{_group.Name}\" settings updated.",
                    Duration = 4000,
                }
            );
        }
        catch (Exception ex)
        {
            NotificationService.Notify(
                new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Save Failed",
                    Detail = ex.Message,
                    Duration = 6000,
                }
            );
        }
    }

    private void ResetSettings()
    {
        if (_group is null)
            return;

        _group.MaxActiveJobs = _savedMaxActiveJobs;
        _group.Priority = _savedPriority;
        _group.IsEnabled = _savedIsEnabled;
    }

    private void OnManifestRowClick(DataGridRowMouseEventArgs<Manifest> args)
    {
        Navigation.NavigateTo($"chainsharp/data/manifests/{args.Data.Id}");
    }

    private void OnMetadataRowClick(DataGridRowMouseEventArgs<Metadata> args)
    {
        Navigation.NavigateTo($"chainsharp/data/metadata/{args.Data.Id}");
    }

    private void OnDagNodeClick(int groupId)
    {
        Navigation.NavigateTo($"chainsharp/data/manifest-groups/{groupId}");
    }
}
