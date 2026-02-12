using ChainSharp.Effect.Dashboard.Services.DashboardSettings;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Settings;

public partial class DashboardSettingsPage
{
    [Inject]
    private IDashboardSettingsService DashboardSettings { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    private int _pollingIntervalSeconds;

    protected override async Task OnInitializedAsync()
    {
        await DashboardSettings.InitializeAsync();
        _pollingIntervalSeconds = (int)DashboardSettings.PollingInterval.TotalSeconds;
    }

    private async Task Save()
    {
        await DashboardSettings.SetPollingIntervalAsync(_pollingIntervalSeconds);

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Settings Saved",
                Detail =
                    $"Polling interval set to {_pollingIntervalSeconds}s. Change takes effect on the next cycle.",
                Duration = 4000,
            }
        );
    }

    private async Task ResetDefault()
    {
        _pollingIntervalSeconds = DashboardSettingsService.DefaultPollingIntervalSeconds;
        await DashboardSettings.SetPollingIntervalAsync(_pollingIntervalSeconds);

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Default Restored",
                Detail =
                    $"Polling interval reset to {DashboardSettingsService.DefaultPollingIntervalSeconds}s.",
                Duration = 4000,
            }
        );
    }
}
