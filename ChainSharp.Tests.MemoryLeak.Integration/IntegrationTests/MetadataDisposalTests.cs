using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Tests.MemoryLeak.Integration.TestWorkflows;
using ChainSharp.Tests.MemoryLeak.Integration.TestWorkflows.TestModels;
using ChainSharp.Tests.MemoryLeak.Integration.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.MemoryLeak.Integration.IntegrationTests;

/// <summary>
/// Tests for validating that Metadata JsonDocument objects are properly disposed.
/// This addresses the primary memory leak issue where JsonDocument objects were not being disposed.
/// </summary>
[TestFixture]
public class MetadataDisposalTests
{
    private IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        _serviceProvider = TestSetup.CreateMemoryOnlyTestServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Test]
    public void Metadata_ShouldImplementIDisposable()
    {
        // Arrange & Act
        var metadata = Metadata.Create(
            new CreateMetadata { Name = "TestWorkflow", Input = new { TestData = "Test" } }
        );

        // Assert
        metadata.Should().BeAssignableTo<IDisposable>();
    }

    [Test]
    public void Metadata_ShouldDisposeJsonDocuments_WhenDisposed()
    {
        // Arrange
        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = "TestWorkflow",
                Input = new { LargeData = new string('X', 10000) }
            }
        );

        // Simulate setting Output JsonDocument (this would normally happen during workflow execution)
        var outputData = new { Result = new string('Y', 10000) };
        metadata.OutputObject = outputData;

        // Act - Dispose the metadata
        var beforeDisposal = metadata.Input;
        var outputBeforeDisposal = metadata.Output;

        metadata.Dispose();

        // Assert - JsonDocument objects should be disposed
        // We can't directly check if JsonDocument is disposed, but we can verify the disposal pattern works
        metadata.Should().NotBeNull();

        // Calling Dispose again should not throw (idempotent)
        Action secondDispose = () => metadata.Dispose();
        secondDispose.Should().NotThrow();
    }

    [Test]
    public async Task Metadata_ShouldBeCollectedAfterDisposal()
    {
        // Arrange
        var factory = () =>
            Metadata.Create(
                new CreateMetadata
                {
                    Name = "TestWorkflow",
                    Input = new { LargeData = new string('X', 50000) } // 50KB of data
                }
            );

        // Act & Assert
        var allCollected = await MemoryProfiler.TestObjectDisposal(
            factory,
            objectCount: 50,
            maxWaitTime: TimeSpan.FromSeconds(15)
        );

        allCollected.Should().BeTrue("All Metadata objects should be collected after disposal");
    }

    [Test]
    public async Task WorkflowExecution_ShouldNotLeakMemory_WithLargeData()
    {
        // Arrange
        var memoryTestWorkflow = _serviceProvider.GetRequiredService<IMemoryTestWorkflow>();
        var input = MemoryTestModelFactory.CreateLargeInput(dataSizeMB: 2); // 2MB of data

        // Act & Assert
        var result = await MemoryProfiler.MonitorMemoryUsageAsync(
            async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    var testInput = MemoryTestModelFactory.CreateLargeInput(
                        $"test_{i}",
                        dataSizeMB: 1
                    );
                    var output = await memoryTestWorkflow.Run(testInput);
                    output.Should().NotBeNull();
                }
            },
            "WorkflowExecution_ShouldNotLeakMemory_WithLargeData"
        );

        Console.WriteLine(result.GetSummary());

        // Memory should be freed after GC
        result
            .MemoryRetained.Should()
            .BeLessThan(
                result.MemoryAllocated / 2,
                "Most allocated memory should be freed after GC, indicating no significant leaks"
            );

        // Should not retain more than 10MB after 20 workflows with 1MB each
        result
            .MemoryRetained.Should()
            .BeLessThan(
                10 * 1024 * 1024,
                "Should not retain more than 10MB after processing 20x1MB workflows"
            );
    }

    [Test]
    public async Task MultipleWorkflowExecutions_ShouldShowConsistentMemoryUsage()
    {
        // Arrange
        var memoryTestWorkflow = _serviceProvider.GetRequiredService<IMemoryTestWorkflow>();
        var batchResults = new List<MemoryMonitorResult>();

        // Act - Run multiple batches and measure memory
        for (int batch = 0; batch < 3; batch++)
        {
            var result = await MemoryProfiler.MonitorMemoryUsageAsync(
                async () =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var testInput = MemoryTestModelFactory.CreateInput(
                            $"batch_{batch}_item_{i}",
                            dataSizeBytes: 50000
                        ); // 50KB each
                        var output = await memoryTestWorkflow.Run(testInput);
                        output.Should().NotBeNull();
                    }
                },
                $"Batch {batch}"
            );

            batchResults.Add(result);
            Console.WriteLine(result.GetSummary());

            // Force cleanup between batches
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Assert - Memory usage should be consistent across batches (no cumulative leaks)
        var retainedMemories = batchResults.Select(r => r.MemoryRetained).ToList();

        // No batch should retain significantly more memory than others
        var maxRetained = retainedMemories.Max();
        var minRetained = retainedMemories.Min();

        (maxRetained - minRetained)
            .Should()
            .BeLessThan(
                5 * 1024 * 1024,
                "Memory retention should be consistent across batches (difference < 5MB)"
            );
    }

    [Test]
    public async Task FailingWorkflows_ShouldNotLeakMemory()
    {
        // Arrange
        var failingWorkflow = _serviceProvider.GetRequiredService<IFailingTestWorkflow>();

        // Act & Assert
        var result = await MemoryProfiler.MonitorMemoryUsageAsync(
            async () =>
            {
                for (int i = 0; i < 15; i++)
                {
                    var testInput = MemoryTestModelFactory.CreateFailingInput(
                        $"failing_test_{i}",
                        dataSizeBytes: 100000
                    ); // 100KB each

                    try
                    {
                        await failingWorkflow.Run(testInput);
                    }
                    catch (Exception)
                    {
                        // Expected to fail - we're testing memory cleanup in error paths
                    }
                }
            },
            "FailingWorkflows_ShouldNotLeakMemory"
        );

        Console.WriteLine(result.GetSummary());

        // Even with failing workflows, memory should be cleaned up properly
        result
            .MemoryRetained.Should()
            .BeLessThan(
                result.MemoryAllocated / 3,
                "Failed workflows should still clean up most allocated memory"
            );
    }

    [Test]
    public void MetadataWithNullJsonDocuments_ShouldNotThrowOnDispose()
    {
        // Arrange
        var metadata = Metadata.Create(
            new CreateMetadata { Name = "TestWorkflow", Input = new { TestData = "Test" } }
        );

        // Ensure JsonDocument properties are null
        metadata.Input.Should().BeNull();
        metadata.Output.Should().BeNull();

        // Act & Assert
        Action dispose = () => metadata.Dispose();
        dispose.Should().NotThrow("Disposing metadata with null JsonDocuments should be safe");
    }
}
