namespace ChainSharp.Effect.Dashboard.Services.DashboardSettings;

public interface IDashboardSettingsService
{
    TimeSpan PollingInterval { get; }
    DateTime LastPollTime { get; }
    Task InitializeAsync();
    Task SetPollingIntervalAsync(int seconds);
    void NotifyPolled();
}
