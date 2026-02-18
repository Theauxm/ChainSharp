using ChainSharp.Effect.Dashboard.Components.Shared;
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

        _group = await context.ManifestGroups.FirstOrDefaultAsync(
            g => g.Id == ManifestGroupId,
            cancellationToken
        );

        if (_group is null)
        {
            _manifests = [];
            _executions = [];
            return;
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

        SnapshotSettings();
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
}
