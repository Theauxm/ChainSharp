using System.Text.Json;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Dashboard.Components.Shared;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Log;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Orchestration.Scheduler.Services.BackgroundTaskServer;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Radzen;
using static ChainSharp.Effect.Dashboard.Utilities.DashboardFormatters;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Data;

public partial class MetadataDetailPage
{
    [Inject]
    private IDataContextProviderFactory DataContextFactory { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IBackgroundTaskServer BackgroundTaskServer { get; set; } = default!;

    [Inject]
    private IWorkflowDiscoveryService WorkflowDiscovery { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public int MetadataId { get; set; }

    private Metadata? _metadata;
    private List<Log> _logs = [];
    private bool _rerunning;
    private string? _rerunError;
    private int _previousMetadataId;

    protected override async Task OnParametersSetAsync()
    {
        if (_previousMetadataId != 0 && _previousMetadataId != MetadataId)
        {
            IsLoading = true;
            await LoadDataAsync(CancellationToken.None);
            IsLoading = false;
            StateHasChanged();
        }

        _previousMetadataId = MetadataId;
    }

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        _metadata = await context
            .Metadatas.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == MetadataId, cancellationToken);

        if (_metadata is not null)
        {
            _logs = await context
                .Logs.AsNoTracking()
                .Where(l => l.MetadataId == MetadataId)
                .ToListAsync(cancellationToken);
        }
    }

    private async Task RerunWorkflow()
    {
        if (_metadata is null || string.IsNullOrWhiteSpace(_metadata.Input))
            return;

        _rerunError = null;
        _rerunning = true;

        try
        {
            var registration = WorkflowDiscovery
                .DiscoverWorkflows()
                .FirstOrDefault(
                    r =>
                        r.ServiceType.FullName == _metadata.Name
                        || r.ImplementationType.FullName == _metadata.Name
                );

            if (registration is null)
            {
                _rerunError =
                    $"No workflow registration found for '{ShortName(_metadata.Name)}'. Is the workflow still registered?";
                return;
            }

            var input = JsonSerializer.Deserialize(
                _metadata.Input,
                registration.InputType,
                ChainSharpEffectConfiguration.StaticSystemJsonSerializerOptions
            );

            if (input is null)
            {
                _rerunError = "Failed to deserialize the saved input.";
                return;
            }

            var metadata = Metadata.Create(
                new CreateMetadata
                {
                    Name = registration.ServiceType.FullName!,
                    ExternalId = Guid.NewGuid().ToString("N"),
                    Input = null,
                }
            );

            using var dataContext = await DataContextFactory.CreateDbContextAsync(
                CancellationToken.None
            );
            await dataContext.Track(metadata);
            await dataContext.SaveChanges(CancellationToken.None);

            await BackgroundTaskServer.EnqueueAsync(metadata.Id, input);

            NotificationService.Notify(
                NotificationSeverity.Success,
                "Workflow Queued",
                $"{ShortName(_metadata.Name)} has been re-queued (ID {metadata.Id}).",
                duration: 4000
            );

            Navigation.NavigateTo($"chainsharp/data/metadata/{metadata.Id}");
        }
        catch (JsonException je)
        {
            _rerunError = $"Invalid saved input JSON: {je.Message}";
        }
        catch (Exception ex)
        {
            _rerunError = ex.Message;
        }
        finally
        {
            _rerunning = false;
        }
    }
}
