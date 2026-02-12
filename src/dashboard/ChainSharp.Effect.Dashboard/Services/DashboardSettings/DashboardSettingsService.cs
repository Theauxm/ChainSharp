using ChainSharp.Effect.Dashboard.Services.LocalStorage;

namespace ChainSharp.Effect.Dashboard.Services.DashboardSettings;

public class DashboardSettingsService(ILocalStorageService localStorage) : IDashboardSettingsService
{
    public const int DefaultPollingIntervalSeconds = 15;

    private bool _isInitialized;

    public TimeSpan PollingInterval { get; private set; } =
        TimeSpan.FromSeconds(DefaultPollingIntervalSeconds);

    public DateTime LastPollTime { get; private set; } = DateTime.UtcNow;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        var stored = await localStorage.GetAsync<int?>(StorageKeys.PollingInterval);
        if (stored is > 0)
            PollingInterval = TimeSpan.FromSeconds(stored.Value);

        _isInitialized = true;
    }

    public async Task SetPollingIntervalAsync(int seconds)
    {
        seconds = Math.Max(1, seconds);
        PollingInterval = TimeSpan.FromSeconds(seconds);
        await localStorage.SetAsync(StorageKeys.PollingInterval, seconds);
    }

    public void NotifyPolled()
    {
        LastPollTime = DateTime.UtcNow;
    }
}
