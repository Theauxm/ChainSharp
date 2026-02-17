using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestPollingService;

/// <summary>
/// Background service that polls for due manifests on a configurable interval.
/// </summary>
/// <remarks>
/// This service replaces the previous Hangfire recurring job approach, enabling sub-minute
/// polling intervals via <see cref="PeriodicTimer"/>. On startup, it seeds any pending
/// manifests configured via <see cref="SchedulerConfigurationBuilder.Schedule{TWorkflow,TInput}"/>.
///
/// TaskServerExecutor jobs are still enqueued to the configured <see cref="BackgroundTaskServer.IBackgroundTaskServer"/>
/// (e.g., Hangfire) for execution — this service only handles the polling trigger.
/// </remarks>
internal class ManifestPollingService(
    IServiceProvider serviceProvider,
    SchedulerConfiguration configuration,
    ILogger<ManifestPollingService> logger
) : BackgroundService
{
    /// <summary>
    /// Seeds pending manifests during host startup. Failures here prevent the host from starting.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await SeedPendingManifests(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the ManifestManager polling loop on the configured interval.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ManifestPollingService starting with polling interval {Interval}",
            configuration.PollingInterval
        );

        using var timer = new PeriodicTimer(configuration.PollingInterval);

        // Run immediately on startup before waiting for the first tick
        await PollManifests();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollManifests();
        }

        logger.LogInformation("ManifestPollingService stopping");
    }

    private async Task PollManifests()
    {
        using var scope = serviceProvider.CreateScope();

        // Step 1: ManifestManager queues manifests → work queue entries
        try
        {
            var manifestManager =
                scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();

            logger.LogDebug("ManifestManager polling cycle starting");
            await manifestManager.Run(Unit.Default);
            logger.LogDebug("ManifestManager polling cycle completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during ManifestManager polling cycle");
        }

        // Step 2: JobDispatcher picks from queue → creates metadata → enqueues
        try
        {
            var jobDispatcher = scope.ServiceProvider.GetRequiredService<IJobDispatcherWorkflow>();

            logger.LogDebug("JobDispatcher polling cycle starting");
            await jobDispatcher.Run(Unit.Default);
            logger.LogDebug("JobDispatcher polling cycle completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during JobDispatcher polling cycle");
        }
    }

    private async Task SeedPendingManifests(CancellationToken cancellationToken)
    {
        if (configuration.PendingManifests.Count == 0)
            return;

        logger.LogInformation(
            "Seeding {Count} pending manifest(s) from startup configuration...",
            configuration.PendingManifests.Count
        );

        using var scope = serviceProvider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IManifestScheduler>();

        foreach (var pending in configuration.PendingManifests)
        {
            try
            {
                await pending.ScheduleFunc(scheduler, cancellationToken);
                logger.LogDebug("Seeded manifest: {ExternalId}", pending.ExternalId);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to seed manifest {ExternalId}: {Message}",
                    pending.ExternalId,
                    ex.Message
                );
                throw;
            }
        }

        logger.LogInformation(
            "Successfully seeded {Count} manifest(s)",
            configuration.PendingManifests.Count
        );
    }
}
