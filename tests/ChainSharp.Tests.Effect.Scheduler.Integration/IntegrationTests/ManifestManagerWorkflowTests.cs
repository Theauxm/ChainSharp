using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.DeadLetter.DTOs;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Tests.Effect.Scheduler.Integration.Examples.Workflows;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Scheduler.Integration.IntegrationTests;

/// <summary>
/// Integration tests for the ManifestManagerWorkflow which orchestrates the manifest-based job scheduling system.
/// </summary>
/// <remarks>
/// The ManifestManagerWorkflow runs through the following steps:
/// 1. LoadManifestsStep - Loads all enabled manifests with their Metadatas and DeadLetters
/// 2. ReapFailedJobsStep - Creates DeadLetter records for manifests exceeding retry limits
/// 3. DetermineJobsToQueueStep - Determines which manifests are due for execution
/// 4. EnqueueJobsStep - Creates Metadata records and enqueues jobs to the background task server
/// </remarks>
[TestFixture]
public class ManifestManagerWorkflowTests : TestSetup
{
    private IManifestManagerWorkflow _workflow = null!;

    public override async Task TestSetUp()
    {
        await base.TestSetUp();
        _workflow = Scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();

        // Ensure test isolation by disabling all existing manifests
        // This prevents interference from data created by previous test runs
        await CleanupExistingManifests();
    }

    /// <summary>
    /// Disables all existing enabled manifests to ensure test isolation.
    /// Each test creates its own manifests, and only those should be processed.
    /// </summary>
    private async Task CleanupExistingManifests()
    {
        var existingManifests = await DataContext.Manifests.Where(m => m.IsEnabled).ToListAsync();

        foreach (var manifest in existingManifests)
        {
            manifest.IsEnabled = false;
        }

        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();
    }

    [TearDown]
    public async Task ManifestManagerWorkflowTestsTearDown()
    {
        if (_workflow is IDisposable disposable)
            disposable.Dispose();
    }

    #region LoadManifestsStep Tests

    [Test]
    public async Task Run_WithEnabledManifest_LoadsManifest()
    {
        // Arrange - Create an enabled manifest
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            isEnabled: true
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - The workflow should complete without errors
        // Since the manifest is interval-based and never ran, it should be queued
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().NotBeEmpty("the manifest should have been queued for execution");
    }

    [Test]
    public async Task Run_WithDisabledManifest_DoesNotLoadManifest()
    {
        // Arrange - Create a disabled manifest
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            isEnabled: false
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No metadata should be created for the disabled manifest
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().BeEmpty("disabled manifests should not be processed");
    }

    #endregion

    #region ReapFailedJobsStep Tests

    [Test]
    public async Task Run_WhenManifestExceedsMaxRetries_CreatesDeadLetter()
    {
        // Arrange - Create a manifest with max_retries = 2 and 3 failed executions
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            maxRetries: 2
        );

        // Create 3 failed metadata records
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - A dead letter should be created
        DataContext.Reset();
        var deadLetters = await DataContext
            .DeadLetters.Where(dl => dl.ManifestId == manifest.Id)
            .ToListAsync();

        deadLetters.Should().HaveCount(1);
        deadLetters[0].Status.Should().Be(DeadLetterStatus.AwaitingIntervention);
        deadLetters[0].Reason.Should().Contain("Max retries exceeded");
    }

    [Test]
    public async Task Run_WhenManifestHasNotExceededMaxRetries_DoesNotCreateDeadLetter()
    {
        // Arrange - Create a manifest with max_retries = 3 and only 2 failed executions
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            maxRetries: 3
        );

        // Create 2 failed metadata records (below the threshold)
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No dead letter should be created
        DataContext.Reset();
        var deadLetters = await DataContext
            .DeadLetters.Where(dl => dl.ManifestId == manifest.Id)
            .ToListAsync();

        deadLetters.Should().BeEmpty("manifest has not exceeded max retries");
    }

    [Test]
    public async Task Run_WhenManifestAlreadyHasAwaitingInterventionDeadLetter_DoesNotCreateDuplicateDeadLetter()
    {
        // Arrange - Create a manifest with an existing dead letter
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            maxRetries: 2
        );

        // Create failed metadata records
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(manifest, WorkflowState.Failed);

        // Create an existing dead letter
        await CreateAndSaveDeadLetter(manifest, DeadLetterStatus.AwaitingIntervention);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No duplicate dead letter should be created
        DataContext.Reset();
        var deadLetters = await DataContext
            .DeadLetters.Where(dl => dl.ManifestId == manifest.Id)
            .ToListAsync();

        deadLetters.Should().HaveCount(1, "should not create duplicate dead letters");
    }

    #endregion

    #region DetermineJobsToQueueStep Tests

    [Test]
    public async Task Run_WhenIntervalManifestIsDue_EnqueuesJob()
    {
        // Arrange - Create an interval manifest that's never run (immediately due)
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - A metadata record should be created for the job
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().HaveCount(1, "interval manifest should be queued");

        // Verify execution happened via LastSuccessfulRun being updated
        // (InMemoryTaskServer executes immediately)
        var updatedManifest = await DataContext.Manifests.FirstOrDefaultAsync(
            m => m.Id == manifest.Id
        );
        updatedManifest!
            .LastSuccessfulRun.Should()
            .NotBeNull("InMemoryTaskServer executes immediately");
        updatedManifest
            .LastSuccessfulRun.Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Run_WhenIntervalManifestNotYetDue_DoesNotEnqueueJob()
    {
        // Arrange - Create an interval manifest that ran recently
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 3600 // 1 hour
        );

        // Re-load the manifest to track it and update LastSuccessfulRun
        var trackedManifest = await DataContext.Manifests.FirstAsync(m => m.Id == manifest.Id);
        trackedManifest.LastSuccessfulRun = DateTime.UtcNow;
        await DataContext.SaveChanges(CancellationToken.None);
        var lastRunBefore = trackedManifest.LastSuccessfulRun!.Value;
        DataContext.Reset();

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No new metadata should be created and LastSuccessfulRun unchanged
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().BeEmpty("interval has not elapsed yet");

        // Verify LastSuccessfulRun wasn't changed (within milliseconds due to DB precision)
        var updatedManifest = await DataContext.Manifests.FirstAsync(m => m.Id == manifest.Id);
        updatedManifest
            .LastSuccessfulRun.Should()
            .BeCloseTo(
                lastRunBefore,
                TimeSpan.FromMilliseconds(100),
                "manifest should not have been executed"
            );
    }

    [Test]
    public async Task Run_WhenCronManifestIsDue_EnqueuesJob()
    {
        // Arrange - Create a cron manifest with "every minute" expression that never ran
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Cron,
            cronExpression: "* * * * *" // every minute
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - A metadata record should be created
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().HaveCount(1, "cron manifest that never ran should be queued");
    }

    [Test]
    public async Task Run_WhenManifestHasScheduleTypeNone_DoesNotEnqueueJob()
    {
        // Arrange - Create a manifest with ScheduleType.None (manual only)
        var manifest = await CreateAndSaveManifest(scheduleType: ScheduleType.None);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No metadata should be created
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().BeEmpty("ScheduleType.None manifests should not be auto-scheduled");
    }

    [Test]
    public async Task Run_WhenManifestHasOnDemandSchedule_DoesNotAutoEnqueue()
    {
        // Arrange - Create an OnDemand manifest
        var manifest = await CreateAndSaveManifest(scheduleType: ScheduleType.OnDemand);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No metadata should be created (OnDemand is for bulk operations only)
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas
            .Should()
            .BeEmpty("OnDemand manifests should only be triggered via BulkEnqueueAsync");
    }

    [Test]
    public async Task Run_WhenManifestHasAwaitingInterventionDeadLetter_DoesNotEnqueueJob()
    {
        // Arrange - Create a manifest with a dead letter
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        await CreateAndSaveDeadLetter(manifest, DeadLetterStatus.AwaitingIntervention);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No metadata should be created
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas
            .Should()
            .BeEmpty("manifests with AwaitingIntervention dead letters should be skipped");
    }

    [Test]
    public async Task Run_WhenManifestHasPendingExecution_DoesNotEnqueueJob()
    {
        // Arrange - Create a manifest with a pending execution
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        await CreateAndSaveMetadata(manifest, WorkflowState.Pending);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No new metadata should be created
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas
            .Should()
            .HaveCount(1, "should not queue a manifest that already has pending execution");
        metadatas[0].WorkflowState.Should().Be(WorkflowState.Pending);
    }

    [Test]
    public async Task Run_WhenManifestHasInProgressExecution_DoesNotEnqueueJob()
    {
        // Arrange - Create a manifest with an in-progress execution
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        await CreateAndSaveMetadata(manifest, WorkflowState.InProgress);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No new metadata should be created
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas
            .Should()
            .HaveCount(1, "should not queue a manifest that has in-progress execution");
        metadatas[0].WorkflowState.Should().Be(WorkflowState.InProgress);
    }

    #endregion

    #region EnqueueJobsStep Tests

    [Test]
    public async Task Run_WhenManifestIsQueued_CreatesMetadataWithCorrectManifestId()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();
        var metadata = await DataContext.Metadatas.FirstOrDefaultAsync(
            m => m.ManifestId == manifest.Id
        );

        metadata.Should().NotBeNull();
        metadata!.ManifestId.Should().Be(manifest.Id);
    }

    [Test]
    public async Task Run_WhenManifestIsQueued_ExecutesWorkflowViaInMemoryTaskServer()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - InMemoryTaskServer executes immediately and updates LastSuccessfulRun
        DataContext.Reset();
        var updatedManifest = await DataContext.Manifests.FirstOrDefaultAsync(
            m => m.Id == manifest.Id
        );

        updatedManifest.Should().NotBeNull();
        updatedManifest!.LastSuccessfulRun.Should().NotBeNull();
        updatedManifest
            .LastSuccessfulRun.Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    #endregion

    #region Full Workflow Integration Tests

    [Test]
    public async Task Run_WithMultipleManifests_ProcessesEachCorrectly()
    {
        // Arrange - Create multiple manifests with different scenarios
        var enabledIntervalManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "Enabled_Interval"
        );

        var disabledManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            isEnabled: false,
            inputValue: "Disabled"
        );

        var manualOnlyManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.None,
            inputValue: "ManualOnly"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();

        // Enabled interval manifest should be queued and executed
        var enabledMetadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == enabledIntervalManifest.Id)
            .ToListAsync();
        enabledMetadatas.Should().HaveCount(1);

        // Disabled manifest should not be processed
        var disabledMetadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == disabledManifest.Id)
            .ToListAsync();
        disabledMetadatas.Should().BeEmpty();

        // Manual-only manifest should not be auto-scheduled
        var manualMetadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manualOnlyManifest.Id)
            .ToListAsync();
        manualMetadatas.Should().BeEmpty();
    }

    [Test]
    public async Task Run_WithMixOfFailedAndHealthyManifests_DeadLettersCorrectly()
    {
        // Arrange
        var failingManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            maxRetries: 2,
            inputValue: "Failing"
        );

        // Add 3 failed executions (exceeds max_retries of 2)
        await CreateAndSaveMetadata(failingManifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(failingManifest, WorkflowState.Failed);
        await CreateAndSaveMetadata(failingManifest, WorkflowState.Failed);

        var healthyManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "Healthy"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();

        // Failing manifest should have a dead letter and no new executions
        var failingDeadLetters = await DataContext
            .DeadLetters.Where(dl => dl.ManifestId == failingManifest.Id)
            .ToListAsync();
        failingDeadLetters.Should().HaveCount(1);
        failingDeadLetters[0].Status.Should().Be(DeadLetterStatus.AwaitingIntervention);

        var failingNewMetadatas = await DataContext
            .Metadatas.Where(
                m => m.ManifestId == failingManifest.Id && m.WorkflowState == WorkflowState.Pending
            )
            .ToListAsync();
        failingNewMetadatas.Should().BeEmpty("dead-lettered manifests should not be queued");

        // Healthy manifest should be queued and executed
        var healthyMetadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == healthyManifest.Id)
            .ToListAsync();
        healthyMetadatas.Should().HaveCount(1);
    }

    [Test]
    public async Task Run_CompletesSuccessfully_ReturnsUnit()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60
        );

        // Act & Assert - The workflow should complete successfully without throwing
        var act = async () => await _workflow.Run(Unit.Default);
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Run_WithNoEnabledManifests_CompletesWithoutErrors()
    {
        // Arrange - Create only disabled manifests
        await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            isEnabled: false
        );

        // Act & Assert - Should complete without throwing
        var act = async () => await _workflow.Run(Unit.Default);
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Run_AfterCompletedExecution_DoesNotQueueAgainIfIntervalNotElapsed()
    {
        // Arrange - Create an interval manifest with a long interval
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 3600 // 1 hour
        );

        // Act - Run the workflow once
        await _workflow.Run(Unit.Default);

        // Assert - After one run, LastSuccessfulRun should be set
        // The next poll would not queue this manifest again because the interval hasn't elapsed
        DataContext.Reset();
        var updatedManifest = await DataContext.Manifests.FirstOrDefaultAsync(
            m => m.Id == manifest.Id
        );

        updatedManifest.Should().NotBeNull();
        updatedManifest!
            .LastSuccessfulRun.Should()
            .NotBeNull(
                "LastSuccessfulRun should be set after successful execution, preventing re-queue until interval elapses"
            );
        updatedManifest
            .LastSuccessfulRun.Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Run_WhenCronExpressionIsInvalid_DoesNotCrash()
    {
        // Arrange - Create a manifest with an invalid cron expression
        var manifest = await CreateAndSaveManifestRaw(
            scheduleType: ScheduleType.Cron,
            cronExpression: "invalid-cron",
            intervalSeconds: null
        );

        // Act - Should not throw
        var act = async () => await _workflow.Run(Unit.Default);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Run_WhenIntervalSecondsIsZero_DoesNotEnqueue()
    {
        // Arrange - Create a manifest with zero interval
        var manifest = await CreateAndSaveManifestRaw(
            scheduleType: ScheduleType.Interval,
            cronExpression: null,
            intervalSeconds: 0
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Should not create metadata due to invalid interval
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().BeEmpty("zero interval should be treated as invalid");
    }

    [Test]
    public async Task Run_WhenIntervalSecondsIsNegative_DoesNotEnqueue()
    {
        // Arrange - Create a manifest with negative interval
        var manifest = await CreateAndSaveManifestRaw(
            scheduleType: ScheduleType.Interval,
            cronExpression: null,
            intervalSeconds: -100
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().BeEmpty("negative interval should be treated as invalid");
    }

    [Test]
    public async Task Run_WhenCronExpressionIsNull_DoesNotEnqueue()
    {
        // Arrange - Create a cron manifest without a cron expression
        var manifest = await CreateAndSaveManifestRaw(
            scheduleType: ScheduleType.Cron,
            cronExpression: null,
            intervalSeconds: null
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();
        var metadatas = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().BeEmpty("cron manifest without expression should not be queued");
    }

    #endregion

    #region Helper Methods

    private async Task<Manifest> CreateAndSaveManifest(
        ScheduleType scheduleType = ScheduleType.None,
        int? intervalSeconds = null,
        string? cronExpression = null,
        int maxRetries = 3,
        bool isEnabled = true,
        string inputValue = "TestValue"
    )
    {
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(SchedulerTestWorkflow),
                IsEnabled = isEnabled,
                ScheduleType = scheduleType,
                IntervalSeconds = intervalSeconds,
                CronExpression = cronExpression,
                MaxRetries = maxRetries,
                Properties = new SchedulerTestInput { Value = inputValue }
            }
        );

        await DataContext.Track(manifest);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return manifest;
    }

    /// <summary>
    /// Creates a manifest with raw values for edge case testing.
    /// </summary>
    private async Task<Manifest> CreateAndSaveManifestRaw(
        ScheduleType scheduleType,
        string? cronExpression,
        int? intervalSeconds,
        bool isEnabled = true
    )
    {
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(SchedulerTestWorkflow),
                IsEnabled = isEnabled,
                ScheduleType = scheduleType,
                IntervalSeconds = intervalSeconds,
                CronExpression = cronExpression,
                MaxRetries = 3,
                Properties = new SchedulerTestInput { Value = "EdgeCase" }
            }
        );

        await DataContext.Track(manifest);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return manifest;
    }

    private async Task<Metadata> CreateAndSaveMetadata(Manifest manifest, WorkflowState state)
    {
        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = typeof(SchedulerTestWorkflow).FullName!,
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = manifest.GetProperties<SchedulerTestInput>(),
                ManifestId = manifest.Id
            }
        );

        metadata.WorkflowState = state;

        await DataContext.Track(metadata);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return metadata;
    }

    private async Task<DeadLetter> CreateAndSaveDeadLetter(
        Manifest manifest,
        DeadLetterStatus status
    )
    {
        // Reload the manifest from the current DataContext to avoid EF tracking issues
        var reloadedManifest = await DataContext.Manifests.FirstAsync(m => m.Id == manifest.Id);

        var deadLetter = DeadLetter.Create(
            new CreateDeadLetter
            {
                Manifest = reloadedManifest,
                Reason = "Test dead letter",
                RetryCount = 3
            }
        );

        deadLetter.Status = status;

        await DataContext.Track(deadLetter);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return deadLetter;
    }

    #endregion
}
