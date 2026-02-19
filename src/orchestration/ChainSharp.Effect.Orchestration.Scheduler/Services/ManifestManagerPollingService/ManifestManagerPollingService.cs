using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestManagerPollingService;

/// <summary>
/// Background service that polls for due manifests on a configurable interval
/// and runs <see cref="IManifestManagerWorkflow"/> each cycle.
/// </summary>
internal class ManifestManagerPollingService(
    IServiceProvider serviceProvider,
    SchedulerConfiguration configuration,
    ILogger<ManifestManagerPollingService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ManifestManagerPollingService starting with polling interval {Interval}",
            configuration.ManifestManagerPollingInterval
        );

        using var timer = new PeriodicTimer(configuration.ManifestManagerPollingInterval);

        await RunManifestManager();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunManifestManager();
        }

        logger.LogInformation("ManifestManagerPollingService stopping");
    }

    private async Task RunManifestManager()
    {
        if (!configuration.ManifestManagerEnabled)
        {
            logger.LogDebug("ManifestManager is disabled, skipping polling cycle");
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();

            logger.LogDebug("ManifestManager polling cycle starting");
            await workflow.Run(Unit.Default);
            logger.LogDebug("ManifestManager polling cycle completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during ManifestManager polling cycle");
        }
    }
}
