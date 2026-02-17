using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Manifest;
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

    [Parameter]
    public string GroupId { get; set; } = "";

    private List<Manifest> _manifests = [];
    private List<Metadata> _executions = [];

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        var decodedGroupId = Uri.UnescapeDataString(GroupId);

        _manifests = await context
            .Manifests.AsNoTracking()
            .Where(m => m.GroupId == decodedGroupId)
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
