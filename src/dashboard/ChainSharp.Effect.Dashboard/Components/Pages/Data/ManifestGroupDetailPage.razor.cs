using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Dashboard.Models;
using ChainSharp.Effect.Dashboard.Utilities;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
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

    private ManifestGroup? _group;
    private List<Manifest> _manifests = [];
    private List<Metadata> _executions = [];
    private DagLayout? _dagLayout;

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
            _manifests = [];
            _executions = [];
            _dagLayout = null;
            return;
        }

        // Don't overwrite the user's unsaved edits during poll ticks
        if (!IsSettingsDirty)
        {
            _group = freshGroup;
            SnapshotSettings();
        }

        _manifests = await context
            .Manifests.AsNoTracking()
            .Where(m => m.ManifestGroupId == ManifestGroupId)
            .ToListAsync(cancellationToken);

        if (_manifests.Count > 0)
        {
            var manifestIds = _manifests.Select(m => m.Id).ToList();

            _executions = await context
                .Metadatas.AsNoTracking()
                .Where(m => m.ManifestId.HasValue && manifestIds.Contains(m.ManifestId.Value))
                .OrderByDescending(m => m.StartTime)
                .Take(200)
                .ToListAsync(cancellationToken);
        }

        // Build 1-hop neighborhood dependency graph
        await LoadDependencyGraph(context, cancellationToken);
    }

    private async Task LoadDependencyGraph(
        Effect.Data.Services.DataContext.IDataContext context,
        CancellationToken cancellationToken
    )
    {
        var currentManifestIds = _manifests.Select(m => m.Id).ToList();

        if (currentManifestIds.Count == 0)
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
                    && currentManifestIds.Contains(m.DependsOnManifestId.Value)
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
