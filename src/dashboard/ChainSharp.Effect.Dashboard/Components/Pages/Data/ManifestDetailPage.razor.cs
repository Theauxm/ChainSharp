using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;
using static ChainSharp.Effect.Dashboard.Utilities.DashboardFormatters;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Data;

public partial class ManifestDetailPage
{
    [Inject]
    private IDataContextProviderFactory DataContextFactory { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IManifestScheduler ManifestScheduler { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public int ManifestId { get; set; }

    protected override object? GetRouteKey() => ManifestId;

    private Manifest? _manifest;
    private List<Metadata> _metadataItems = [];
    private bool _triggering;
    private string? _triggerError;

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        _manifest = await context
            .Manifests.Include(m => m.ManifestGroup)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == ManifestId, cancellationToken);

        if (_manifest is not null)
        {
            _metadataItems = await context
                .Metadatas.AsNoTracking()
                .Where(m => m.ManifestId == ManifestId)
                .OrderByDescending(m => m.StartTime)
                .ToListAsync(cancellationToken);
        }
    }

    private async Task TriggerManifest()
    {
        if (_manifest is null)
            return;

        _triggerError = null;
        _triggering = true;

        try
        {
            await ManifestScheduler.TriggerAsync(_manifest.ExternalId);

            NotificationService.Notify(
                NotificationSeverity.Success,
                "Workflow Queued",
                $"{ShortName(_manifest.Name)} has been queued for execution.",
                duration: 4000
            );
        }
        catch (Exception ex)
        {
            _triggerError = ex.Message;
        }
        finally
        {
            _triggering = false;
        }
    }
}
