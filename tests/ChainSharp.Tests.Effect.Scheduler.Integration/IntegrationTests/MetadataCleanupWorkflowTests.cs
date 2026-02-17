using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Log;
using ChainSharp.Effect.Models.Log.DTOs;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Orchestration.Scheduler.Configuration;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.MetadataCleanup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.Effect.Scheduler.Integration.IntegrationTests;

/// <summary>
/// Integration tests for the MetadataCleanupWorkflow which deletes expired metadata
/// entries for whitelisted workflow types.
/// </summary>
[TestFixture]
public class MetadataCleanupWorkflowTests : TestSetup
{
    private IMetadataCleanupWorkflow _workflow = null!;
    private SchedulerConfiguration _config = null!;

    public override async Task TestSetUp()
    {
        await base.TestSetUp();
        _workflow = Scope.ServiceProvider.GetRequiredService<IMetadataCleanupWorkflow>();
        _config = Scope.ServiceProvider.GetRequiredService<SchedulerConfiguration>();
    }

    [TearDown]
    public async Task MetadataCleanupWorkflowTestsTearDown()
    {
        if (_workflow is IDisposable disposable)
            disposable.Dispose();
    }

    #region Default Configuration Tests

    [Test]
    public void DefaultWhitelist_ContainsManifestManagerWorkflow()
    {
        _config
            .MetadataCleanup!.WorkflowTypeWhitelist.Should()
            .Contain(
                typeof(ManifestManagerWorkflow).FullName!,
                "ManifestManagerWorkflow should be in the default whitelist"
            );
    }

    [Test]
    public void DefaultWhitelist_ContainsMetadataCleanupWorkflow()
    {
        _config
            .MetadataCleanup!.WorkflowTypeWhitelist.Should()
            .Contain(
                typeof(MetadataCleanupWorkflow).FullName!,
                "MetadataCleanupWorkflow should be in the default whitelist"
            );
    }

    [Test]
    public void DefaultRetentionPeriod_IsThirtyMinutes()
    {
        _config
            .MetadataCleanup!.RetentionPeriod.Should()
            .Be(TimeSpan.FromMinutes(30), "default retention period should be 30 minutes");
    }

    [Test]
    public void DefaultCleanupInterval_IsOneMinute()
    {
        _config
            .MetadataCleanup!.CleanupInterval.Should()
            .Be(TimeSpan.FromMinutes(1), "default cleanup interval should be 1 minute");
    }

    #endregion

    #region Deletion Tests - Terminal States

    [Test]
    public async Task Run_DeletesExpiredCompletedMetadata()
    {
        // Arrange - Create completed metadata older than retention period
        var metadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remaining = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remaining.Should().BeNull("expired completed metadata should be deleted");
    }

    [Test]
    public async Task Run_DeletesExpiredFailedMetadata()
    {
        // Arrange - Create failed metadata older than retention period
        var metadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Failed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remaining = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remaining.Should().BeNull("expired failed metadata should be deleted");
    }

    #endregion

    #region Retention Period Tests

    [Test]
    public async Task Run_DoesNotDeleteRecentMetadata()
    {
        // Arrange - Create completed metadata within retention period
        var metadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddMinutes(-15) // 15 min ago, within 30 minute retention
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remaining = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remaining
            .Should()
            .NotBeNull("recent metadata within retention period should not be deleted");
    }

    #endregion

    #region Whitelist Filtering Tests

    [Test]
    public async Task Run_DoesNotDeleteNonWhitelistedMetadata()
    {
        // Arrange - Create old completed metadata for a non-whitelisted workflow
        var metadata = await CreateAndSaveMetadata(
            name: "SomeOtherWorkflow",
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remaining = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remaining
            .Should()
            .NotBeNull("metadata for non-whitelisted workflows should not be deleted");
    }

    [Test]
    public async Task Run_DeletesMetadataForAllWhitelistedTypes()
    {
        // Arrange - Create expired metadata for both default whitelisted types
        var managerMetadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        var cleanupMetadata = await CreateAndSaveMetadata(
            name: typeof(MetadataCleanupWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var managerRemaining = await DataContext
            .Metadatas.Where(m => m.Id == managerMetadata.Id)
            .FirstOrDefaultAsync();
        var cleanupRemaining = await DataContext
            .Metadatas.Where(m => m.Id == cleanupMetadata.Id)
            .FirstOrDefaultAsync();

        managerRemaining
            .Should()
            .BeNull("expired ManifestManagerWorkflow metadata should be deleted");
        cleanupRemaining
            .Should()
            .BeNull("expired MetadataCleanupWorkflow metadata should be deleted");
    }

    #endregion

    #region Non-Terminal State Tests

    [Test]
    public async Task Run_DoesNotDeletePendingMetadata()
    {
        // Arrange - Create old pending metadata (non-terminal state)
        var metadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Pending,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remaining = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remaining.Should().NotBeNull("pending metadata should never be deleted regardless of age");
    }

    [Test]
    public async Task Run_DoesNotDeleteInProgressMetadata()
    {
        // Arrange - Create old in-progress metadata (non-terminal state)
        var metadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.InProgress,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remaining = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remaining
            .Should()
            .NotBeNull("in-progress metadata should never be deleted regardless of age");
    }

    #endregion

    #region Associated Logs Tests

    [Test]
    public async Task Run_DeletesAssociatedLogs()
    {
        // Arrange - Create expired metadata with associated logs
        var metadata = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        var log = Log.Create(
            new CreateLog
            {
                Level = LogLevel.Information,
                Message = "Test log entry",
                CategoryName = "TestCategory",
                EventId = 1,
            }
        );

        // Set MetadataId by tracking the log in the context
        await DataContext.Logs.AddAsync(log);

        // Use raw SQL to set the metadata_id since MetadataId has a private setter
        await DataContext.SaveChanges(CancellationToken.None);
        var logId = log.Id;

        await DataContext
            .Logs.Where(l => l.Id == logId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.MetadataId, metadata.Id));

        DataContext.Reset();

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();
        var remainingLog = await DataContext.Logs.Where(l => l.Id == logId).FirstOrDefaultAsync();
        var remainingMetadata = await DataContext
            .Metadatas.Where(m => m.Id == metadata.Id)
            .FirstOrDefaultAsync();

        remainingLog
            .Should()
            .BeNull("logs associated with deleted metadata should also be deleted");
        remainingMetadata.Should().BeNull("the metadata itself should be deleted");
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Run_WithNoExpiredMetadata_CompletesSuccessfully()
    {
        // Act & Assert - Should complete without throwing
        var act = async () => await _workflow.Run(new MetadataCleanupRequest());
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Run_WithMixOfEligibleAndIneligibleMetadata_DeletesOnlyEligible()
    {
        // Arrange
        var expiredWhitelisted = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        var recentWhitelisted = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddMinutes(-10)
        );

        var expiredNonWhitelisted = await CreateAndSaveMetadata(
            name: "SomeOtherWorkflow",
            state: WorkflowState.Completed,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        var expiredPending = await CreateAndSaveMetadata(
            name: typeof(ManifestManagerWorkflow).FullName!,
            state: WorkflowState.Pending,
            startTime: DateTime.UtcNow.AddHours(-2)
        );

        // Act
        await _workflow.Run(new MetadataCleanupRequest());

        // Assert
        DataContext.Reset();

        var deletedCheck = await DataContext
            .Metadatas.Where(m => m.Id == expiredWhitelisted.Id)
            .FirstOrDefaultAsync();
        deletedCheck.Should().BeNull("expired whitelisted completed metadata should be deleted");

        var recentCheck = await DataContext
            .Metadatas.Where(m => m.Id == recentWhitelisted.Id)
            .FirstOrDefaultAsync();
        recentCheck.Should().NotBeNull("recent metadata should survive");

        var nonWhitelistedCheck = await DataContext
            .Metadatas.Where(m => m.Id == expiredNonWhitelisted.Id)
            .FirstOrDefaultAsync();
        nonWhitelistedCheck.Should().NotBeNull("non-whitelisted metadata should survive");

        var pendingCheck = await DataContext
            .Metadatas.Where(m => m.Id == expiredPending.Id)
            .FirstOrDefaultAsync();
        pendingCheck.Should().NotBeNull("pending metadata should survive");
    }

    #endregion

    #region Configuration Tests

    [Test]
    public void AddWorkflowType_Generic_AddsTypeName()
    {
        var config = new MetadataCleanupConfiguration();
        config.AddWorkflowType<ManifestManagerWorkflow>();

        config.WorkflowTypeWhitelist.Should().Contain(typeof(ManifestManagerWorkflow).FullName!);
    }

    [Test]
    public void AddWorkflowType_String_AddsName()
    {
        var config = new MetadataCleanupConfiguration();
        config.AddWorkflowType("CustomWorkflow");

        config.WorkflowTypeWhitelist.Should().Contain("CustomWorkflow");
    }

    [Test]
    public void AddWorkflowType_CanAppendMultipleTypes()
    {
        var config = new MetadataCleanupConfiguration();
        config.AddWorkflowType<ManifestManagerWorkflow>();
        config.AddWorkflowType<MetadataCleanupWorkflow>();
        config.AddWorkflowType("ThirdWorkflow");

        config
            .WorkflowTypeWhitelist.Should()
            .HaveCount(3)
            .And.Contain(typeof(ManifestManagerWorkflow).FullName!)
            .And.Contain(typeof(MetadataCleanupWorkflow).FullName!)
            .And.Contain("ThirdWorkflow");
    }

    #endregion

    #region Helper Methods

    private async Task<Metadata> CreateAndSaveMetadata(
        string name,
        WorkflowState state,
        DateTime startTime
    )
    {
        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = name,
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = null,
            }
        );

        metadata.WorkflowState = state;
        metadata.StartTime = startTime;

        if (state is WorkflowState.Completed or WorkflowState.Failed)
            metadata.EndTime = startTime.AddSeconds(1);

        await DataContext.Track(metadata);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return metadata;
    }

    #endregion
}
