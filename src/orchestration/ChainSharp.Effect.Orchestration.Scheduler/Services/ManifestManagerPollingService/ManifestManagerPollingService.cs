using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestManagerPollingService;

/// <summary>
/// Background service that polls for due manifests on a configurable interval
/// and runs <see cref="IManifestManagerWorkflow"/> each cycle.
/// </summary>
/// <remarks>
/// Uses a PostgreSQL advisory lock (<c>pg_try_advisory_xact_lock</c>) to ensure
/// only one server instance runs the manifest evaluation cycle at a time,
/// preventing duplicate WorkQueue entries in multi-server deployments.
/// </remarks>
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
            var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();

            // Advisory lock: single-leader election for manifest evaluation.
            // Prevents duplicate WorkQueue entries when multiple servers poll simultaneously.
            if (dataContext is DbContext dbContext)
            {
                using var transaction = await dataContext.BeginTransaction();

                var acquired = await dbContext
                    .Database.SqlQuery<bool>(
                        $"SELECT pg_try_advisory_xact_lock(hashtext('chainsharp_manifest_manager'))"
                    )
                    .FirstAsync();

                if (!acquired)
                {
                    logger.LogDebug("Another server is running ManifestManager, skipping cycle");
                    await dataContext.RollbackTransaction();
                    return;
                }

                // Run workflow within the advisory lock transaction
                var workflow = scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();

                logger.LogDebug("ManifestManager polling cycle starting");
                await workflow.Run(Unit.Default);
                logger.LogDebug("ManifestManager polling cycle completed");

                await dataContext.CommitTransaction();
            }
            else
            {
                // Non-EF provider (e.g., InMemory for tests) — run without lock
                var workflow = scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();

                logger.LogDebug("ManifestManager polling cycle starting");
                await workflow.Run(Unit.Default);
                logger.LogDebug("ManifestManager polling cycle completed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during ManifestManager polling cycle");
        }
    }
}
