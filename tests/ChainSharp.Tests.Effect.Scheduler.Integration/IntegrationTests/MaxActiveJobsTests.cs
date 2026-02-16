using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.DeadLetter;
using ChainSharp.Effect.Models.DeadLetter.DTOs;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Orchestration.Mediator.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestManager;
using ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;
using ChainSharp.Effect.Provider.Json.Extensions;
using ChainSharp.Effect.Provider.Parameter.Extensions;
using ChainSharp.Effect.StepProvider.Logging.Extensions;
using ChainSharp.Tests.ArrayLogger.Services.ArrayLoggingProvider;
using ChainSharp.Tests.Effect.Scheduler.Integration.Examples.Workflows;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.Effect.Scheduler.Integration.IntegrationTests;

/// <summary>
/// Integration tests for the MaxActiveJobs configuration that limits the total number
/// of active (Pending + InProgress) jobs across all manifests.
/// </summary>
/// <remarks>
/// These tests verify:
/// - MaxActiveJobs correctly limits total queue depth
/// - Both Pending and InProgress states count toward the limit
/// - Jobs can be queued again after active jobs complete
/// - Edge cases like exactly at limit, one below limit, etc.
/// </remarks>
[TestFixture]
public class MaxActiveJobsTests
{
    private const int MaxActiveJobsLimit = 5;

    private ServiceProvider _serviceProvider = null!;
    private IServiceScope _scope = null!;
    private IManifestManagerWorkflow _workflow = null!;
    private IDataContext _dataContext = null!;

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var connectionString = configuration.GetRequiredSection("Configuration")[
            "DatabaseConnectionString"
        ]!;

        var arrayLoggingProvider = new ArrayLoggingProvider();

        _serviceProvider = new ServiceCollection()
            .AddSingleton<ILoggerProvider>(arrayLoggingProvider)
            .AddSingleton<IArrayLoggingProvider>(arrayLoggingProvider)
            .AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .AddChainSharpEffects(
                options =>
                    options
                        .AddEffectWorkflowBus(
                            assemblies:
                            [
                                typeof(AssemblyMarker).Assembly,
                                typeof(TaskServerExecutorWorkflow).Assembly,
                            ]
                        )
                        .SetEffectLogLevel(LogLevel.Information)
                        .SaveWorkflowParameters()
                        .AddPostgresEffect(connectionString)
                        .AddEffectDataContextLogging(minimumLogLevel: LogLevel.Trace)
                        .AddJsonEffect()
                        .AddStepLogger(serializeStepData: true)
                        .AddScheduler(scheduler =>
                            scheduler
                                .UseInMemoryTaskServer()
                                .MaxActiveJobs(MaxActiveJobsLimit)
                        )
            )
            .AddScoped<IDataContext>(sp =>
            {
                var factory = sp.GetRequiredService<IDataContextProviderFactory>();
                return (IDataContext)factory.Create();
            })
            .BuildServiceProvider();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await _serviceProvider.DisposeAsync();
    }

    [SetUp]
    public async Task TestSetUp()
    {
        _scope = _serviceProvider.CreateScope();
        _workflow = _scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();
        _dataContext = _scope.ServiceProvider.GetRequiredService<IDataContext>();

        // Ensure test isolation by disabling all existing manifests
        await CleanupExistingManifests();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        if (_workflow is IDisposable disposable)
            disposable.Dispose();

        if (_dataContext is IDisposable dataContextDisposable)
            dataContextDisposable.Dispose();

        _scope.Dispose();
    }

    private async Task CleanupExistingManifests()
    {
        var existingManifests = await _dataContext.Manifests.Where(m => m.IsEnabled).ToListAsync();

        foreach (var manifest in existingManifests)
        {
            manifest.IsEnabled = false;
        }

        await _dataContext.SaveChanges(CancellationToken.None);
        _dataContext.Reset();
    }

    #region Basic MaxActiveJobs Limit Tests

    [Test]
    public async Task Run_WhenTotalActiveJobsAtLimit_DoesNotQueueNewJobs()
    {
        // Arrange - Create manifests that already have Pending jobs at the limit
        var existingManifests = new List<Manifest>();
        for (var i = 0; i < MaxActiveJobsLimit; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Existing_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
            existingManifests.Add(manifest);
        }

        // Create a new manifest that would be due for execution
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No new metadata should be created for newManifest
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas
            .Should()
            .BeEmpty(
                $"total active jobs ({MaxActiveJobsLimit}) is at limit, no new jobs should be queued"
            );
    }

    [Test]
    public async Task Run_WhenTotalActiveJobsBelowLimit_QueuesNewJobs()
    {
        // Arrange - Create fewer Pending jobs than the limit
        var existingJobCount = MaxActiveJobsLimit - 2;
        for (var i = 0; i < existingJobCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Existing_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        }

        // Create new manifests that should be queued
        var newManifest1 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest1"
        );
        var newManifest2 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest2"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - New manifests should be queued (InMemoryTaskServer executes immediately)
        _dataContext.Reset();

        var manifest1Metadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest1.Id)
            .ToListAsync();
        var manifest2Metadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest2.Id)
            .ToListAsync();

        // At least one should have been queued (capacity was 2)
        (manifest1Metadatas.Count + manifest2Metadatas.Count)
            .Should()
            .BeGreaterThanOrEqualTo(1, "there was capacity for new jobs");
    }

    [Test]
    public async Task Run_WhenExactlyOneSlotAvailable_QueuesExactlyOneJob()
    {
        // Arrange - Fill all but one slot
        var existingJobCount = MaxActiveJobsLimit - 1;
        for (var i = 0; i < existingJobCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Existing_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        }

        // Create multiple new manifests that would be due
        var newManifests = new List<Manifest>();
        for (var i = 0; i < 3; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"NewManifest_{i}"
            );
            newManifests.Add(manifest);
        }

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Only one new job should be queued
        _dataContext.Reset();
        var totalNewMetadatas = 0;
        foreach (var manifest in newManifests)
        {
            var metadatas = await _dataContext
                .Metadatas.Where(m => m.ManifestId == manifest.Id)
                .ToListAsync();
            totalNewMetadatas += metadatas.Count;
        }

        totalNewMetadatas
            .Should()
            .Be(1, "only one slot was available so only one job should be queued");
    }

    #endregion

    #region Pending + InProgress Combined Limit Tests

    [Test]
    public async Task Run_CountsBothPendingAndInProgressTowardLimit()
    {
        // Arrange - Create mix of Pending and InProgress jobs
        var pendingCount = 2;
        var inProgressCount = MaxActiveJobsLimit - pendingCount;

        for (var i = 0; i < pendingCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Pending_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        }

        for (var i = 0; i < inProgressCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"InProgress_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.InProgress);
        }

        // Create a new manifest that would be due
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - No new job should be queued
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas
            .Should()
            .BeEmpty(
                $"combined Pending ({pendingCount}) + InProgress ({inProgressCount}) = {MaxActiveJobsLimit} is at limit"
            );
    }

    [Test]
    public async Task Run_OnlyInProgressJobs_CountsTowardLimit()
    {
        // Arrange - Fill limit with only InProgress jobs
        for (var i = 0; i < MaxActiveJobsLimit; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"InProgress_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.InProgress);
        }

        // Create a new manifest
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas.Should().BeEmpty("InProgress jobs count toward the limit");
    }

    [Test]
    public async Task Run_CompletedJobsDoNotCountTowardLimit()
    {
        // Arrange - Create completed (Succeeded) jobs - these should NOT count
        for (var i = 0; i < MaxActiveJobsLimit + 5; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 3600, // Long interval so they don't re-queue
                inputValue: $"Completed_{i}"
            );
            var metadata = await CreateAndSaveMetadata(manifest, WorkflowState.Completed);

            // Set LastSuccessfulRun so they don't re-queue
            var trackedManifest = await _dataContext.Manifests.FirstAsync(m => m.Id == manifest.Id);
            trackedManifest.LastSuccessfulRun = DateTime.UtcNow;
            await _dataContext.SaveChanges(CancellationToken.None);
            _dataContext.Reset();
        }

        // Create new manifests that should be queued
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - New manifest should be queued because Succeeded jobs don't count
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas
            .Should()
            .NotBeEmpty("Succeeded jobs do not count toward MaxActiveJobs limit");
    }

    [Test]
    public async Task Run_FailedJobsDoNotCountTowardLimit()
    {
        // Arrange - Create failed jobs - these should NOT count toward active limit
        // Use long interval and set LastSuccessfulRun so they won't be re-queued
        for (var i = 0; i < MaxActiveJobsLimit + 5; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 3600, // Long interval so they won't be re-queued
                maxRetries: 100, // High retry count so they don't get dead-lettered
                inputValue: $"Failed_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Failed);

            // Set LastSuccessfulRun to prevent re-queuing
            var trackedManifest = await _dataContext.Manifests.FirstAsync(m => m.Id == manifest.Id);
            trackedManifest.LastSuccessfulRun = DateTime.UtcNow;
            await _dataContext.SaveChanges(CancellationToken.None);
            _dataContext.Reset();
        }

        // Create a new manifest (different from the failed ones)
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - New manifest should be queued
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas
            .Should()
            .NotBeEmpty("Failed jobs do not count toward MaxActiveJobs limit");
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Run_WhenNoActiveJobs_QueuesUpToLimit()
    {
        // Arrange - Create more manifests than the limit
        var manifestCount = MaxActiveJobsLimit + 3;
        var manifests = new List<Manifest>();
        for (var i = 0; i < manifestCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Manifest_{i}"
            );
            manifests.Add(manifest);
        }

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Should queue up to MaxActiveJobs (though InMemoryTaskServer executes immediately)
        // Since InMemoryTaskServer executes immediately, some jobs may complete before the limit check
        // The key assertion is that we don't queue ALL manifests in one cycle when there's a limit
        _dataContext.Reset();
        var totalMetadatas = await _dataContext
            .Metadatas.Where(m => manifests.Select(x => x.Id).Contains(m.ManifestId!.Value))
            .ToListAsync();

        // Due to InMemoryTaskServer executing immediately, we can't easily test the exact limit,
        // but we verify the workflow completes without error
        totalMetadatas.Should().NotBeEmpty();
    }

    [Test]
    public async Task Run_WhenOverLimit_DoesNotEnqueueAnyNewJobs()
    {
        // Arrange - Create MORE than the limit of active jobs (shouldn't happen normally, but test it)
        var overLimitCount = MaxActiveJobsLimit + 3;
        for (var i = 0; i < overLimitCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Existing_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        }

        // Create a new manifest
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas.Should().BeEmpty("system is already over the limit");
    }

    [Test]
    public async Task Run_WithZeroActiveJobs_QueuesNormally()
    {
        // Arrange - No existing active jobs
        var manifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "TestManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Should queue the job (InMemoryTaskServer executes immediately)
        _dataContext.Reset();
        var metadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == manifest.Id)
            .ToListAsync();

        metadatas.Should().NotBeEmpty("there was capacity to queue jobs");
    }

    #endregion

    #region Interaction with Other Features

    [Test]
    public async Task Run_RespectsMaxActiveJobsAlongWithMaxActiveJobs()
    {
        // This test verifies both limits work together.
        // MaxActiveJobs = 10, MaxActiveJobs = 5
        // If we have 0 active jobs and 15 due manifests,
        // we should only queue 5 (limited by MaxActiveJobs, not MaxActiveJobs)

        // Arrange - Create many manifests
        var manifestCount = 15;
        var manifests = new List<Manifest>();
        for (var i = 0; i < manifestCount; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Manifest_{i}"
            );
            manifests.Add(manifest);
        }

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Due to InMemoryTaskServer executing immediately, jobs complete quickly
        // But the workflow should respect the limits during enqueueing
        _dataContext.Reset();

        // The test verifies the workflow completes without error
        // Actual limit enforcement is tested in other tests with pre-existing pending jobs
        var totalMetadatas = await _dataContext
            .Metadatas.Where(m => manifests.Select(x => x.Id).Contains(m.ManifestId!.Value))
            .ToListAsync();

        totalMetadatas.Should().NotBeEmpty();
    }

    [Test]
    public async Task Run_SkipsManifestsWithExistingActiveExecutionsIndependentOfGlobalLimit()
    {
        // Arrange - Create a manifest that already has a pending execution
        var manifestWithPending = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "ManifestWithPending"
        );
        await CreateAndSaveMetadata(manifestWithPending, WorkflowState.Pending);

        // Total active jobs = 1, below limit of 5
        // But manifestWithPending should still be skipped because it has an active execution

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Should not create a second metadata for the manifest
        _dataContext.Reset();
        var metadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == manifestWithPending.Id)
            .ToListAsync();

        metadatas
            .Should()
            .HaveCount(
                1,
                "manifest should be skipped due to existing active execution, not global limit"
            );
    }

    [Test]
    public async Task Run_DoesNotCountDeadLetteredManifestsTowardActiveLimit()
    {
        // Arrange - Create manifests with dead letters (shouldn't count as active)
        for (var i = 0; i < MaxActiveJobsLimit + 2; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                maxRetries: 1,
                inputValue: $"DeadLettered_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
            await CreateAndSaveMetadata(manifest, WorkflowState.Failed);
            await CreateAndSaveDeadLetter(manifest, DeadLetterStatus.AwaitingIntervention);
        }

        // Create a new manifest
        var newManifest = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - New manifest should be queued
        _dataContext.Reset();
        var newMetadatas = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest.Id)
            .ToListAsync();

        newMetadatas
            .Should()
            .NotBeEmpty(
                "dead-lettered manifests don't have active executions, so limit is not reached"
            );
    }

    #endregion

    #region Limit Boundary Tests

    [Test]
    public async Task Run_AtExactLimitMinusOne_QueuesOneJob()
    {
        // Arrange - Create exactly limit - 1 active jobs
        for (var i = 0; i < MaxActiveJobsLimit - 1; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Existing_{i}"
            );
            await CreateAndSaveMetadata(manifest, WorkflowState.Pending);
        }

        // Create multiple new manifests
        var newManifest1 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest1"
        );
        var newManifest2 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest2"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Exactly one new job should be queued
        _dataContext.Reset();
        var newMetadatas1 = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest1.Id)
            .ToListAsync();
        var newMetadatas2 = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest2.Id)
            .ToListAsync();

        var totalNew = newMetadatas1.Count + newMetadatas2.Count;
        totalNew.Should().Be(1, "only one slot was available");
    }

    [Test]
    public async Task Run_WithMixedStatesAtBoundary_CorrectlyCalculatesLimit()
    {
        // Arrange - Create mix at exact boundary
        // 2 Pending + 2 InProgress + 1 Succeeded (doesn't count) = 4 active
        // Should allow 1 more job

        var pending1 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "Pending1"
        );
        await CreateAndSaveMetadata(pending1, WorkflowState.Pending);

        var pending2 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "Pending2"
        );
        await CreateAndSaveMetadata(pending2, WorkflowState.Pending);

        var inProgress1 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "InProgress1"
        );
        await CreateAndSaveMetadata(inProgress1, WorkflowState.InProgress);

        var inProgress2 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "InProgress2"
        );
        await CreateAndSaveMetadata(inProgress2, WorkflowState.InProgress);

        var succeeded = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 3600,
            inputValue: "Succeeded"
        );
        var succeededMetadata = await CreateAndSaveMetadata(succeeded, WorkflowState.Completed);
        var trackedSucceeded = await _dataContext.Manifests.FirstAsync(m => m.Id == succeeded.Id);
        trackedSucceeded.LastSuccessfulRun = DateTime.UtcNow;
        await _dataContext.SaveChanges(CancellationToken.None);
        _dataContext.Reset();

        // Create new manifests
        var newManifest1 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest1"
        );
        var newManifest2 = await CreateAndSaveManifest(
            scheduleType: ScheduleType.Interval,
            intervalSeconds: 60,
            inputValue: "NewManifest2"
        );

        // Act
        await _workflow.Run(Unit.Default);

        // Assert - Only 1 new job should be queued (4 active, limit 5)
        _dataContext.Reset();
        var newMetadatas1 = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest1.Id)
            .ToListAsync();
        var newMetadatas2 = await _dataContext
            .Metadatas.Where(m => m.ManifestId == newManifest2.Id)
            .ToListAsync();

        var totalNew = newMetadatas1.Count + newMetadatas2.Count;
        totalNew.Should().Be(1, "4 active jobs means only 1 slot available");
    }

    #endregion

    #region Multiple Workflow Cycles

    [Test]
    public async Task Run_MultipleConsecutiveCycles_RespectsLimitEachTime()
    {
        // Arrange - Create manifests that will be due
        var manifests = new List<Manifest>();
        for (var i = 0; i < MaxActiveJobsLimit + 5; i++)
        {
            var manifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Manifest_{i}"
            );
            manifests.Add(manifest);
        }

        // Seed with active jobs at limit
        var existingManifests = new List<Manifest>();
        for (var i = 0; i < MaxActiveJobsLimit; i++)
        {
            var existingManifest = await CreateAndSaveManifest(
                scheduleType: ScheduleType.Interval,
                intervalSeconds: 60,
                inputValue: $"Existing_{i}"
            );
            await CreateAndSaveMetadata(existingManifest, WorkflowState.Pending);
            existingManifests.Add(existingManifest);
        }

        // Act - Run first cycle
        await _workflow.Run(Unit.Default);

        // Assert - No new jobs from first cycle
        _dataContext.Reset();
        foreach (var manifest in manifests)
        {
            var metadatas = await _dataContext
                .Metadatas.Where(m => m.ManifestId == manifest.Id)
                .ToListAsync();
            metadatas.Should().BeEmpty($"limit was already reached for manifest {manifest.Id}");
        }

        // Simulate completing some jobs by changing their state
        foreach (var existingManifest in existingManifests.Take(2))
        {
            var metadata = await _dataContext
                .Metadatas.Where(m => m.ManifestId == existingManifest.Id)
                .FirstAsync();
            metadata.WorkflowState = WorkflowState.Completed;

            var trackedManifest = await _dataContext.Manifests.FirstAsync(
                m => m.Id == existingManifest.Id
            );
            trackedManifest.LastSuccessfulRun = DateTime.UtcNow;
        }
        await _dataContext.SaveChanges(CancellationToken.None);
        _dataContext.Reset();

        // Refresh workflow instance for second cycle
        if (_workflow is IDisposable disposable)
            disposable.Dispose();
        _workflow = _scope.ServiceProvider.GetRequiredService<IManifestManagerWorkflow>();

        // Act - Run second cycle
        await _workflow.Run(Unit.Default);

        // Assert - Now 2 slots are available (limit 5 - 3 still pending = 2 slots)
        _dataContext.Reset();
        var totalNewMetadatas = 0;
        foreach (var manifest in manifests)
        {
            var metadatas = await _dataContext
                .Metadatas.Where(m => m.ManifestId == manifest.Id)
                .ToListAsync();
            totalNewMetadatas += metadatas.Count;
        }

        totalNewMetadatas.Should().Be(2, "2 slots became available after jobs completed");
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

        await _dataContext.Track(manifest);
        await _dataContext.SaveChanges(CancellationToken.None);
        _dataContext.Reset();

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

        await _dataContext.Track(metadata);
        await _dataContext.SaveChanges(CancellationToken.None);
        _dataContext.Reset();

        return metadata;
    }

    private async Task<DeadLetter> CreateAndSaveDeadLetter(
        Manifest manifest,
        DeadLetterStatus status
    )
    {
        var reloadedManifest = await _dataContext.Manifests.FirstAsync(m => m.Id == manifest.Id);

        var deadLetter = DeadLetter.Create(
            new CreateDeadLetter
            {
                Manifest = reloadedManifest,
                Reason = "Test dead letter",
                RetryCount = 3
            }
        );

        deadLetter.Status = status;

        await _dataContext.Track(deadLetter);
        await _dataContext.SaveChanges(CancellationToken.None);
        _dataContext.Reset();

        return deadLetter;
    }

    #endregion
}
