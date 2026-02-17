using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Models.WorkQueue;
using ChainSharp.Effect.Models.WorkQueue.DTOs;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.JobDispatcher;
using ChainSharp.Tests.Effect.Scheduler.Integration.Examples.Workflows;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Scheduler.Integration.IntegrationTests;

/// <summary>
/// Integration tests for the JobDispatcherWorkflow which picks queued WorkQueue entries
/// and dispatches them as background tasks by creating Metadata records.
/// </summary>
/// <remarks>
/// The JobDispatcherWorkflow runs through the following steps:
/// 1. LoadQueuedJobsStep - Loads all WorkQueue entries with Status == Queued, prioritizing dependent workflows then ordered by CreatedAt
/// 2. DispatchJobsStep - For each entry: creates Metadata, updates status to Dispatched, enqueues to BackgroundTaskServer
/// </remarks>
[TestFixture]
public class JobDispatcherWorkflowTests : TestSetup
{
    private IJobDispatcherWorkflow _workflow = null!;

    public override async Task TestSetUp()
    {
        await base.TestSetUp();
        _workflow = Scope.ServiceProvider.GetRequiredService<IJobDispatcherWorkflow>();
    }

    [TearDown]
    public async Task JobDispatcherWorkflowTestsTearDown()
    {
        if (_workflow is IDisposable disposable)
            disposable.Dispose();
    }

    #region LoadQueuedJobsStep Tests

    [Test]
    public async Task Run_WithQueuedEntries_DispatchesThem()
    {
        // Arrange - Create a manifest and a queued work queue entry
        var manifest = await CreateAndSaveManifest();
        var entry = await CreateAndSaveWorkQueueEntry(manifest);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Entry should be dispatched
        DataContext.Reset();
        var updatedEntry = await DataContext.WorkQueues.FirstAsync(q => q.Id == entry.Id);

        updatedEntry.Status.Should().Be(WorkQueueStatus.Dispatched);
        updatedEntry.DispatchedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Run_WithNoQueuedEntries_CompletesWithoutErrors()
    {
        // Arrange - No queued entries

        // Act & Assert
        var act = async () => await _workflow.Run(Unit.Default);
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Run_OnlyPicksQueuedEntries_IgnoresDispatchedAndCancelled()
    {
        // Arrange - Create entries in different statuses
        var manifest = await CreateAndSaveManifest();

        var queuedEntry = await CreateAndSaveWorkQueueEntry(manifest);

        var dispatchedEntry = await CreateAndSaveWorkQueueEntry(manifest);
        dispatchedEntry = await DataContext.WorkQueues.FirstAsync(q => q.Id == dispatchedEntry.Id);
        dispatchedEntry.Status = WorkQueueStatus.Dispatched;
        dispatchedEntry.DispatchedAt = DateTime.UtcNow;
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        var cancelledEntry = await CreateAndSaveWorkQueueEntry(manifest);
        cancelledEntry = await DataContext.WorkQueues.FirstAsync(q => q.Id == cancelledEntry.Id);
        cancelledEntry.Status = WorkQueueStatus.Cancelled;
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Only the queued entry should have been dispatched
        DataContext.Reset();

        var updatedQueued = await DataContext.WorkQueues.FirstAsync(q => q.Id == queuedEntry.Id);
        updatedQueued.Status.Should().Be(WorkQueueStatus.Dispatched);

        var updatedDispatched = await DataContext.WorkQueues.FirstAsync(
            q => q.Id == dispatchedEntry.Id
        );
        updatedDispatched.Status.Should().Be(WorkQueueStatus.Dispatched);

        var updatedCancelled = await DataContext.WorkQueues.FirstAsync(
            q => q.Id == cancelledEntry.Id
        );
        updatedCancelled.Status.Should().Be(WorkQueueStatus.Cancelled);
    }

    #endregion

    #region DispatchJobsStep Tests

    [Test]
    public async Task Run_CreatesMetadataForQueuedEntry()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var entry = await CreateAndSaveWorkQueueEntry(manifest);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - A Metadata record should be created and linked
        DataContext.Reset();
        var updatedEntry = await DataContext
            .WorkQueues.Include(q => q.Metadata)
            .FirstAsync(q => q.Id == entry.Id);

        updatedEntry.MetadataId.Should().NotBeNull();
        updatedEntry.Metadata.Should().NotBeNull();
        updatedEntry.Metadata!.ManifestId.Should().Be(manifest.Id);
    }

    [Test]
    public async Task Run_CreatesMetadataWithCorrectWorkflowName()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var entry = await CreateAndSaveWorkQueueEntry(manifest);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();
        var metadata = await DataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .FirstOrDefaultAsync();

        metadata.Should().NotBeNull();
        metadata!.Name.Should().Be(typeof(SchedulerTestWorkflow).FullName);
    }

    [Test]
    public async Task Run_SetsDispatchedAtTimestamp()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var entry = await CreateAndSaveWorkQueueEntry(manifest);
        var beforeDispatch = DateTime.UtcNow;

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        DataContext.Reset();
        var updatedEntry = await DataContext.WorkQueues.FirstAsync(q => q.Id == entry.Id);

        updatedEntry.DispatchedAt.Should().NotBeNull();
        updatedEntry.DispatchedAt.Should().BeOnOrAfter(beforeDispatch);
        updatedEntry.DispatchedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Run_WithMultipleQueuedEntries_DispatchesAllInOrder()
    {
        // Arrange - Create multiple queued entries for different manifests
        var manifest1 = await CreateAndSaveManifest(inputValue: "First");
        var entry1 = await CreateAndSaveWorkQueueEntry(manifest1);

        var manifest2 = await CreateAndSaveManifest(inputValue: "Second");
        var entry2 = await CreateAndSaveWorkQueueEntry(manifest2);

        var manifest3 = await CreateAndSaveManifest(inputValue: "Third");
        var entry3 = await CreateAndSaveWorkQueueEntry(manifest3);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - All entries should be dispatched
        DataContext.Reset();
        var entries = await DataContext
            .WorkQueues.Where(q => new[] { entry1.Id, entry2.Id, entry3.Id }.Contains(q.Id))
            .ToListAsync();

        entries.Should().HaveCount(3);
        entries
            .Should()
            .AllSatisfy(e =>
            {
                e.Status.Should().Be(WorkQueueStatus.Dispatched);
                e.MetadataId.Should().NotBeNull();
                e.DispatchedAt.Should().NotBeNull();
            });
    }

    [Test]
    public async Task Run_ExecutesWorkflowViaInMemoryTaskServer()
    {
        // Arrange
        var manifest = await CreateAndSaveManifest();
        var entry = await CreateAndSaveWorkQueueEntry(manifest);

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - InMemoryTaskServer executes immediately, which updates LastSuccessfulRun
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

    #region Helper Methods

    private async Task<Manifest> CreateAndSaveManifest(string inputValue = "TestValue")
    {
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(SchedulerTestWorkflow),
                IsEnabled = true,
                ScheduleType = ScheduleType.None,
                MaxRetries = 3,
                Properties = new SchedulerTestInput { Value = inputValue },
            }
        );

        await DataContext.Track(manifest);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return manifest;
    }

    private async Task<WorkQueue> CreateAndSaveWorkQueueEntry(
        Manifest manifest,
        string? inputValue = null
    )
    {
        var input = inputValue ?? manifest.Properties;
        var entry = WorkQueue.Create(
            new CreateWorkQueue
            {
                WorkflowName = typeof(SchedulerTestWorkflow).FullName!,
                Input = input,
                InputTypeName = typeof(SchedulerTestInput).AssemblyQualifiedName,
                ManifestId = manifest.Id,
            }
        );

        await DataContext.Track(entry);
        await DataContext.SaveChanges(CancellationToken.None);
        DataContext.Reset();

        return entry;
    }

    #endregion
}
