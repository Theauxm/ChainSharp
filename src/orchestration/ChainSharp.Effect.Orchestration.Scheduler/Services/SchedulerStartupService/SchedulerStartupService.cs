using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Services.ManifestScheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Orchestration.Scheduler.Services.SchedulerStartupService;

/// <summary>
/// One-shot hosted service that runs startup tasks before the polling services begin.
/// </summary>
/// <remarks>
/// Registered first in DI so that .NET's sequential IHostedService startup order
/// guarantees this completes before ManifestManagerPollingService or
/// JobDispatcherPollingService begin polling.
/// </remarks>
internal class SchedulerStartupService(
    IServiceProvider serviceProvider,
    SchedulerConfiguration configuration,
    ILogger<SchedulerStartupService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuration.RecoverStuckJobsOnStartup)
            await RecoverStuckJobs();

        await SeedPendingManifests(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RecoverStuckJobs()
    {
        var serverStartTime = DateTime.UtcNow;

        using var scope = serviceProvider.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();

        var stuckJobs = await dataContext
            .Metadatas.Where(
                m => m.WorkflowState == WorkflowState.InProgress && m.StartTime < serverStartTime
            )
            .ToListAsync();

        if (stuckJobs.Count == 0)
        {
            logger.LogInformation(
                "RecoverStuckJobs: no in-progress jobs found from before server start"
            );
            return;
        }

        foreach (var metadata in stuckJobs)
        {
            metadata.WorkflowState = WorkflowState.Failed;
            metadata.EndTime = DateTime.UtcNow;
            metadata.AddException(
                new InvalidOperationException("Server restarted while job was in progress")
            );
        }

        await dataContext.SaveChanges(CancellationToken.None);

        logger.LogWarning(
            "RecoverStuckJobs: failed {Count} stuck in-progress job(s) from before server start at {ServerStartTime}",
            stuckJobs.Count,
            serverStartTime
        );
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

        // Clean up orphaned ManifestGroups (groups with no manifests remaining)
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var orphanedCount = await dataContext
            .ManifestGroups.Where(g => !g.Manifests.Any())
            .ExecuteDeleteAsync(cancellationToken);

        if (orphanedCount > 0)
            logger.LogInformation("Cleaned up {Count} orphaned manifest group(s)", orphanedCount);

        // Release closures and captured batch lists that are no longer needed
        configuration.PendingManifests.Clear();
    }
}
