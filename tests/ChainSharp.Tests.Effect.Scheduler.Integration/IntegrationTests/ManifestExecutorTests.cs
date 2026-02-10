using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Exceptions;
using ChainSharp.Tests.Effect.Scheduler.Integration.Examples.Workflows;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Tests.Effect.Scheduler.Integration.IntegrationTests;

[TestFixture]
public class ManifestExecutorTests : TestSetup
{
    #region ExecuteAsync - Null Metadata Tests

    [Test]
    public async Task ExecuteAsync_WhenMetadataNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentMetadataId = 999999;

        // Act
        var act = async () => await ManifestExecutor.ExecuteAsync(nonExistentMetadataId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*has not been loaded*");
    }

    #endregion

    #region ExecuteAsync - Invalid State Tests

    [Test]
    public async Task ExecuteAsync_WhenStateIsCompleted_ThrowsWorkflowException()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Completed);

        // Act
        var act = async () => await ManifestExecutor.ExecuteAsync(metadata.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<WorkflowException>()
            .WithMessage("*Cannot execute a job with state Completed*");
    }

    [Test]
    public async Task ExecuteAsync_WhenStateIsFailed_ThrowsWorkflowException()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Failed);

        // Act
        var act = async () => await ManifestExecutor.ExecuteAsync(metadata.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<WorkflowException>()
            .WithMessage("*Cannot execute a job with state Failed*");
    }

    [Test]
    public async Task ExecuteAsync_WhenStateIsInProgress_ThrowsWorkflowException()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.InProgress);

        // Act
        var act = async () => await ManifestExecutor.ExecuteAsync(metadata.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<WorkflowException>()
            .WithMessage("*Cannot execute a job with state InProgress*");
    }

    #endregion

    #region ExecuteAsync - Null Manifest Tests

    [Test]
    public async Task ExecuteAsync_WhenManifestIsNull_ThrowsInvalidOperationException()
    {
        // Arrange - Create metadata without a manifest
        var metadata = Metadata.Create(new CreateMetadata
        {
            Name = typeof(SchedulerTestWorkflow).FullName!,
            ExternalId = Guid.NewGuid().ToString("N"),
            Input = new SchedulerTestInput { Value = "Test" },
            ManifestId = null
        });

        await DataContext.Track(metadata);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        // Act
        var act = async () => await ManifestExecutor.ExecuteAsync(metadata.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*has not been loaded*");
    }

    #endregion

    #region ExecuteAsync - Successful Execution Tests

    [Test]
    public async Task ExecuteAsync_WhenStateIsPending_ExecutesWorkflowSuccessfully()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        var metadataId = metadata.Id;

        // Act
        await ManifestExecutor.ExecuteAsync(metadataId, CancellationToken.None);

        // Assert - Verify execution happened (LastSuccessfulRun updated)
        DataContext.Reset();
        var updatedManifest = await DataContext.Manifests
            .FirstOrDefaultAsync(x => x.Id == manifest.Id);

        updatedManifest.Should().NotBeNull();
        updatedManifest!.LastSuccessfulRun.Should().NotBeNull();
        updatedManifest.LastSuccessfulRun.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ExecuteAsync_WhenSuccessful_UpdatesLastSuccessfulRunOnManifest()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var beforeExecution = DateTime.UtcNow;
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Pending);

        // Act
        await ManifestExecutor.ExecuteAsync(metadata.Id, CancellationToken.None);
        var afterExecution = DateTime.UtcNow;

        // Assert
        DataContext.Reset();
        var updatedManifest = await DataContext.Manifests
            .FirstOrDefaultAsync(x => x.Id == manifest.Id);

        updatedManifest.Should().NotBeNull();
        updatedManifest!.LastSuccessfulRun.Should().NotBeNull();
        updatedManifest.LastSuccessfulRun.Should().BeOnOrAfter(beforeExecution);
        updatedManifest.LastSuccessfulRun.Should().BeOnOrBefore(afterExecution.AddSeconds(1));
    }

    [Test]
    public async Task ExecuteAsync_WithDifferentInputValues_ExecutesCorrectly()
    {
        // Arrange
        var testValue = $"UniqueValue_{Guid.NewGuid():N}";
        var manifest = await CreateAndSaveManifest(testValue);
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Pending);

        // Act
        await ManifestExecutor.ExecuteAsync(metadata.Id, CancellationToken.None);

        // Assert
        DataContext.Reset();
        var updatedManifest = await DataContext.Manifests
            .FirstOrDefaultAsync(x => x.Id == manifest.Id);

        updatedManifest.Should().NotBeNull();
        updatedManifest!.LastSuccessfulRun.Should().NotBeNull();
    }

    #endregion

    #region ExecuteAsync - Cancellation Tests

    [Test]
    public async Task ExecuteAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = async () => await ManifestExecutor.ExecuteAsync(metadata.Id, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Helper Methods

    private async Task<Manifest> CreateAndSaveManifest(string inputValue = "TestValue")
    {
        var manifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(SchedulerTestWorkflow),
            IsEnabled = true,
            ScheduleType = ScheduleType.None,
            MaxRetries = 3,
            Properties = new SchedulerTestInput { Value = inputValue }
        });

        await DataContext.Track(manifest);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return manifest;
    }

    private async Task<Metadata> CreateAndSaveMetadata(Manifest manifest, WorkflowState state)
    {
        var metadata = Metadata.Create(new CreateMetadata
        {
            Name = typeof(SchedulerTestWorkflow).FullName!,
            ExternalId = Guid.NewGuid().ToString("N"),
            Input = manifest.GetProperties<SchedulerTestInput>(),
            ManifestId = manifest.Id
        });

        metadata.WorkflowState = state;

        await DataContext.Track(metadata);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return metadata;
    }

    #endregion
}
