using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Metadata;
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

    [Parameter]
    public int ManifestId { get; set; }

    private Manifest? _manifest;
    private List<Metadata> _metadataItems = [];

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        _manifest = await context
            .Manifests.AsNoTracking()
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

    private void OnMetadataRowClick(DataGridRowMouseEventArgs<Metadata> args)
    {
        Navigation.NavigateTo($"chainsharp/data/metadata/{args.Data.Id}");
    }
}
