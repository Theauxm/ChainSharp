namespace ChainSharp.Effect.Dashboard.Services.DashboardSettings;

public interface IDashboardSettingsService
{
    TimeSpan PollingInterval { get; }
    DateTime LastPollTime { get; }
    bool HideAdminWorkflows { get; }
    IReadOnlyList<string> AdminWorkflowNames { get; }
    Task InitializeAsync();
    Task SetPollingIntervalAsync(int seconds);
    Task SetHideAdminWorkflowsAsync(bool hide);
    void NotifyPolled();
}
