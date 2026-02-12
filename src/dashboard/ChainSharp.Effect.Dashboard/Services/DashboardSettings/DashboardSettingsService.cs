using ChainSharp.Effect.Dashboard.Services.LocalStorage;

namespace ChainSharp.Effect.Dashboard.Services.DashboardSettings;

public class DashboardSettingsService(ILocalStorageService localStorage) : IDashboardSettingsService
{
    public const int DefaultPollingIntervalSeconds = 15;
    public const bool DefaultHideAdminWorkflows = false;

    public static readonly IReadOnlyList<string> DefaultAdminWorkflowNames =
    [
        "ManifestManagerWorkflow",
        "ManifestExecutorWorkflow",
        "MetadataCleanupWorkflow",
    ];

    private bool _isInitialized;

    public TimeSpan PollingInterval { get; private set; } =
        TimeSpan.FromSeconds(DefaultPollingIntervalSeconds);

    public DateTime LastPollTime { get; private set; } = DateTime.UtcNow;

    public bool HideAdminWorkflows { get; private set; } = DefaultHideAdminWorkflows;

    public IReadOnlyList<string> AdminWorkflowNames => DefaultAdminWorkflowNames;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        var stored = await localStorage.GetAsync<int?>(StorageKeys.PollingInterval);
        if (stored is > 0)
            PollingInterval = TimeSpan.FromSeconds(stored.Value);

        var hideAdmin = await localStorage.GetAsync<bool?>(StorageKeys.HideAdminWorkflows);
        if (hideAdmin.HasValue)
            HideAdminWorkflows = hideAdmin.Value;

        _isInitialized = true;
    }

    public async Task SetPollingIntervalAsync(int seconds)
    {
        seconds = Math.Max(1, seconds);
        PollingInterval = TimeSpan.FromSeconds(seconds);
        await localStorage.SetAsync(StorageKeys.PollingInterval, seconds);
    }

    public async Task SetHideAdminWorkflowsAsync(bool hide)
    {
        HideAdminWorkflows = hide;
        await localStorage.SetAsync(StorageKeys.HideAdminWorkflows, hide);
    }

    public void NotifyPolled()
    {
        LastPollTime = DateTime.UtcNow;
    }
}
