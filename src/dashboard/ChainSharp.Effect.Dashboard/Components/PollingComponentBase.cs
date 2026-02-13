using ChainSharp.Effect.Dashboard.Services.DashboardSettings;
using Microsoft.AspNetCore.Components;

namespace ChainSharp.Effect.Dashboard.Components;

/// <summary>
/// Base component that polls for data on a configurable interval.
/// Subclasses override <see cref="LoadDataAsync"/> with their query logic.
/// The first load shows <see cref="IsLoading"/> = true; subsequent refreshes are silent.
/// The polling interval is read from <see cref="IDashboardSettingsService"/> each cycle,
/// so changes from the settings page take effect on the next tick automatically.
/// </summary>
public abstract class PollingComponentBase : ComponentBase, IAsyncDisposable
{
    [Inject]
    protected IDashboardSettingsService DashboardSettings { get; set; } = default!;

    private CancellationTokenSource? _cts;

    protected bool IsLoading { get; set; } = true;

    protected abstract Task LoadDataAsync(CancellationToken cancellationToken);

    protected override async Task OnInitializedAsync()
    {
        _cts = new CancellationTokenSource();

        await DashboardSettings.InitializeAsync();

        await LoadDataAsync(_cts.Token);
        DashboardSettings.NotifyPolled();
        IsLoading = false;

        _ = PollAsync(_cts.Token);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(DashboardSettings.PollingInterval, ct);

                try
                {
                    await InvokeAsync(async () =>
                    {
                        await LoadDataAsync(ct);
                        DashboardSettings.NotifyPolled();
                        StateHasChanged();
                    });
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Transient failure — skip this tick, retry on next interval.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Component disposed — exit gracefully.
        }
    }

    public virtual ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
