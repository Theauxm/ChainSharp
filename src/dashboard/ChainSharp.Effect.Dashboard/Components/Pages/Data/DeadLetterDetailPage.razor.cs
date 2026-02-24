using System.Text.Json;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Models.WorkQueue.DTOs;
using ChainSharp.Effect.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;
using static ChainSharp.Effect.Dashboard.Utilities.DashboardFormatters;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Data;

public partial class DeadLetterDetailPage
{
    [Inject]
    private IDataContextProviderFactory DataContextFactory { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IWorkflowDiscoveryService WorkflowDiscovery { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public long DeadLetterId { get; set; }

    private DeadLetter? _deadLetter;
    private List<Metadata> _failedRuns = [];
    private Metadata? _latestFailedRun;

    private bool _requeueing;
    private bool _acknowledging;
    private bool _showAcknowledgeInput;
    private string _acknowledgeNote = "";
    private string? _actionError;

    protected override object? GetRouteKey() => DeadLetterId;

    protected override async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var context = await DataContextFactory.CreateDbContextAsync(cancellationToken);

        _deadLetter = await context
            .DeadLetters.Include(d => d.Manifest)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == DeadLetterId, cancellationToken);

        if (_deadLetter is not null)
        {
            _failedRuns = await context
                .Metadatas.AsNoTracking()
                .Where(
                    m =>
                        m.ManifestId == _deadLetter.ManifestId
                        && m.WorkflowState == WorkflowState.Failed
                )
                .OrderByDescending(m => m.StartTime)
                .ToListAsync(cancellationToken);

            _latestFailedRun = _failedRuns.FirstOrDefault();
        }
    }

    private async Task RequeueManifest()
    {
        if (_deadLetter?.Manifest is null)
            return;

        _actionError = null;
        _requeueing = true;

        try
        {
            var manifest = _deadLetter.Manifest;

            var registration = WorkflowDiscovery
                .DiscoverWorkflows()
                .FirstOrDefault(
                    r =>
                        r.ServiceType.FullName == manifest.Name
                        || r.ImplementationType.FullName == manifest.Name
                );

            if (registration is null)
            {
                _actionError =
                    $"No workflow registration found for '{ShortName(manifest.Name)}'. Is the workflow still registered?";
                return;
            }

            string? serializedInput = null;
            string? inputTypeName = null;

            if (!string.IsNullOrWhiteSpace(manifest.Properties))
            {
                var deserializedInput = JsonSerializer.Deserialize(
                    manifest.Properties,
                    registration.InputType,
                    ChainSharpJsonSerializationOptions.ManifestProperties
                );

                if (deserializedInput is not null)
                {
                    serializedInput = JsonSerializer.Serialize(
                        deserializedInput,
                        registration.InputType,
                        ChainSharpJsonSerializationOptions.ManifestProperties
                    );
                    inputTypeName = registration.InputType.FullName;
                }
            }

            var entry = WorkQueue.Create(
                new CreateWorkQueue
                {
                    WorkflowName = manifest.Name,
                    Input = serializedInput,
                    InputTypeName = inputTypeName,
                    ManifestId = manifest.Id,
                    Priority = manifest.Priority,
                }
            );

            using var dataContext = await DataContextFactory.CreateDbContextAsync(
                CancellationToken.None
            );

            await dataContext.Track(entry);

            // Only update the dead letter record when it's still blocking the ManifestManager.
            // Already-resolved dead letters (Retried/Acknowledged) are just audit records —
            // re-queuing from them creates a fresh WorkQueue entry without mutating history.
            if (_deadLetter.Status == DeadLetterStatus.AwaitingIntervention)
            {
                var trackedDeadLetter = await dataContext.DeadLetters.FirstAsync(
                    d => d.Id == DeadLetterId
                );

                trackedDeadLetter.Status = DeadLetterStatus.Retried;
                trackedDeadLetter.ResolvedAt = DateTime.UtcNow;
                trackedDeadLetter.ResolutionNote =
                    $"Re-queued via dashboard (WorkQueue {entry.Id})";
            }

            await dataContext.SaveChanges(CancellationToken.None);

            NotificationService.Notify(
                NotificationSeverity.Success,
                "Workflow Re-queued",
                $"{ShortName(manifest.Name)} has been re-queued (WorkQueue ID {entry.Id}).",
                duration: 4000
            );

            Navigation.NavigateTo($"chainsharp/data/work-queue/{entry.Id}");
        }
        catch (JsonException je)
        {
            _actionError = $"Invalid manifest properties JSON: {je.Message}";
        }
        catch (Exception ex)
        {
            _actionError = ex.Message;
        }
        finally
        {
            _requeueing = false;
        }
    }

    private async Task AcknowledgeDeadLetter()
    {
        if (_deadLetter is null)
            return;

        _actionError = null;
        _acknowledging = true;

        try
        {
            using var dataContext = await DataContextFactory.CreateDbContextAsync(
                CancellationToken.None
            );

            var trackedDeadLetter = await dataContext.DeadLetters.FirstAsync(
                d => d.Id == DeadLetterId
            );

            trackedDeadLetter.Acknowledge(_acknowledgeNote);
            await dataContext.SaveChanges(CancellationToken.None);

            _showAcknowledgeInput = false;
            _acknowledgeNote = "";

            NotificationService.Notify(
                NotificationSeverity.Success,
                "Dead Letter Acknowledged",
                $"Dead letter #{DeadLetterId} has been acknowledged.",
                duration: 4000
            );

            // Reload to reflect updated status
            await LoadDataAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _actionError = ex.Message;
        }
        finally
        {
            _acknowledging = false;
        }
    }
}
