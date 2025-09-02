using System.Text.Json;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Json.Services.JsonEffect;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Parameter.Services.ParameterEffectProviderFactory;
using ChainSharp.Effect.Services.EffectProviderFactory;
using ChainSharp.Effect.Services.EffectRunner;
using ChainSharp.Tests.MemoryLeak.Integration.TestWorkflows.TestModels;
using ChainSharp.Tests.MemoryLeak.Integration.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ChainSharp.Tests.MemoryLeak.Integration.IntegrationTests;

/// <summary>
/// Tests for validating memory management across multiple components working together.
/// These tests focus on cross-component memory interactions that could cause leaks
/// when multiple effect providers, workflows, and services interact.
/// </summary>
[TestFixture]
public class CrossComponentMemoryTests
{
    private ServiceProvider _serviceProvider;
    private JsonSerializerOptions _jsonOptions;
    private IChainSharpEffectConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(_jsonOptions);
        _serviceProvider = services.BuildServiceProvider();

        // Create a mock configuration for JsonEffectProvider
        _configuration = new MockChainSharpEffectConfiguration(_jsonOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task EffectProviders_Together_ShouldNotLeakMemory_UnderStress()
    {
        // Test multiple effect providers working together
        var result = await MemoryProfiler.MonitorMemoryUsageAsync(
            async () =>
            {
                for (int iteration = 0; iteration < 30; iteration++)
                {
                    // Create effect providers directly
                    using var parameterEffect = new ParameterEffect(_jsonOptions);
                    using var jsonEffect = new JsonEffectProvider(
                        _serviceProvider.GetService<ILogger<JsonEffectProvider>>(),
                        _configuration
                    );

                    // Create multiple metadata objects to test both providers
                    for (int metadataIndex = 0; metadataIndex < 6; metadataIndex++)
                    {
                        var input = MemoryTestModelFactory.CreateInput(
                            id: $"CrossComponent_{iteration}_{metadataIndex}",
                            dataSizeBytes: 4000,
                            description: $"Cross-component test {iteration}_{metadataIndex}"
                        );

                        var output = new MemoryTestOutput
                        {
                            Id = input.Id,
                            ProcessedAt = DateTime.UtcNow,
                            ProcessedData = new string('X', 3000), // 3KB
                            Success = true,
                            Message = $"Cross-component test output {iteration}_{metadataIndex}"
                        };

                        var metadata = new Metadata
                        {
                            Name = $"CrossComponentMetadata_{iteration}_{metadataIndex}",
                            InputObject = input,
                            OutputObject = output
                        };

                        // Track with both providers
                        await parameterEffect.Track(metadata);
                        await jsonEffect.Track(metadata);

                        // Save changes on both providers
                        await parameterEffect.SaveChanges(CancellationToken.None);
                        await jsonEffect.SaveChanges(CancellationToken.None);
                    }

                    // Force periodic GC to test cleanup
                    if (iteration % 10 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }

                // Final cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            },
            "CrossComponent_MultipleProviders_Stress"
        );

        Console.WriteLine(result.GetSummary());

        // Should not retain excessive memory from cross-component interactions
        result
            .MemoryRetained.Should()
            .BeLessThan(
                15 * 1024 * 1024,
                "Cross-component integration should not cause excessive memory retention"
            );

        // Most memory should be properly cleaned up
        result
            .MemoryRetained.Should()
            .BeLessThan(
                4_000_000,
                "Memory retained should be manageable after cross-component disposal"
            );
    }

    [Test]
    public async Task EffectRunner_WithMultipleProviders_ShouldNotLeakMemory()
    {
        // Test EffectRunner with multiple provider factories
        var result = await MemoryProfiler.MonitorMemoryUsageAsync(
            async () =>
            {
                for (int iteration = 0; iteration < 25; iteration++)
                {
                    // Create provider factories
                    var parameterFactory = new TestParameterEffectProviderFactory(_jsonOptions);
                    var jsonFactory = new TestJsonEffectProviderFactory(
                        _jsonOptions,
                        _configuration
                    );

                    var providerFactories = new List<IEffectProviderFactory>
                    {
                        parameterFactory,
                        jsonFactory
                    };

                    using var effectRunner = new EffectRunner(
                        providerFactories,
                        _serviceProvider.GetService<ILogger<EffectRunner>>()
                    );

                    // Create and track multiple metadata objects
                    for (int metadataIndex = 0; metadataIndex < 8; metadataIndex++)
                    {
                        var input = MemoryTestModelFactory.CreateInput(
                            id: $"EffectRunner_{iteration}_{metadataIndex}",
                            dataSizeBytes: 3500,
                            description: $"Effect runner test {iteration}_{metadataIndex}"
                        );

                        var output = new MemoryTestOutput
                        {
                            Id = input.Id,
                            ProcessedAt = DateTime.UtcNow,
                            ProcessedData = new string('R', 2500), // 2.5KB
                            Success = true,
                            Message = $"Effect runner output {iteration}_{metadataIndex}"
                        };

                        var metadata = new Metadata
                        {
                            Name = $"EffectRunnerMetadata_{iteration}_{metadataIndex}",
                            InputObject = input,
                            OutputObject = output
                        };

                        await effectRunner.Track(metadata);
                    }

                    // Save all changes through runner
                    await effectRunner.SaveChanges(CancellationToken.None);

                    // Periodic cleanup
                    if (iteration % 8 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }

                // Final cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            },
            "CrossComponent_EffectRunner_Memory"
        );

        Console.WriteLine(result.GetSummary());

        // EffectRunner should properly coordinate disposal across providers
        result
            .MemoryRetained.Should()
            .BeLessThan(
                12 * 1024 * 1024,
                "EffectRunner should properly coordinate disposal across providers"
            );

        // Memory should be efficiently managed through the runner
        result
            .MemoryRetained.Should()
            .BeLessThan(3_500_000, "Memory should be efficiently managed through EffectRunner");
    }

    [Test]
    public async Task CrossComponent_WithExceptions_ShouldCleanupMemory()
    {
        // Test memory cleanup when some operations fail
        var result = await MemoryProfiler.MonitorMemoryUsageAsync(
            async () =>
            {
                var exceptionCount = 0;

                for (int iteration = 0; iteration < 35; iteration++)
                {
                    try
                    {
                        using var parameterEffect = new ParameterEffect(_jsonOptions);
                        using var jsonEffect = new JsonEffectProvider(
                            _serviceProvider.GetService<ILogger<JsonEffectProvider>>(),
                            _configuration
                        );

                        var input = MemoryTestModelFactory.CreateFailingInput(
                            id: $"ExceptionTest_{iteration}",
                            dataSizeBytes: 3000,
                            description: $"Exception test {iteration}"
                        );

                        var output = new MemoryTestOutput
                        {
                            Id = input.Id,
                            ProcessedAt = DateTime.UtcNow,
                            ProcessedData = new string('E', 2000), // 2KB
                            Success = iteration % 3 != 0, // Fail every 3rd iteration
                            Message = $"Exception test output {iteration}"
                        };

                        var metadata = new Metadata
                        {
                            Name = $"ExceptionMetadata_{iteration}",
                            InputObject = input,
                            OutputObject = output
                        };

                        await parameterEffect.Track(metadata);
                        await jsonEffect.Track(metadata);

                        // Simulate failure scenario
                        if (iteration % 3 == 0)
                        {
                            throw new InvalidOperationException(
                                $"Simulated failure for iteration {iteration}"
                            );
                        }

                        await parameterEffect.SaveChanges(CancellationToken.None);
                        await jsonEffect.SaveChanges(CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        exceptionCount++;
                        // Memory should still be cleaned up properly through using statements
                    }

                    // Periodic cleanup
                    if (iteration % 12 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }

                Console.WriteLine($"Handled {exceptionCount} exceptions during memory test");

                // Final cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            },
            "CrossComponent_ExceptionPath_Memory"
        );

        Console.WriteLine(result.GetSummary());

        // Exception scenarios should not cause memory leaks
        result
            .MemoryRetained.Should()
            .BeLessThan(10 * 1024 * 1024, "Exception scenarios should not cause memory leaks");

        // Memory should be properly cleaned up even when exceptions occur
        result
            .MemoryRetained.Should()
            .BeLessThan(3_200_000, "Memory should be cleaned up properly even with exceptions");
    }

    [Test]
    public async Task ConcurrentCrossComponent_ShouldNotLeakMemory()
    {
        // Test concurrent execution of multiple components
        var result = await MemoryProfiler.MonitorMemoryUsageAsync(
            async () =>
            {
                var tasks = Enumerable
                    .Range(0, 6) // 6 concurrent tasks
                    .Select(async taskId =>
                    {
                        for (int iteration = 0; iteration < 15; iteration++)
                        {
                            using var parameterEffect = new ParameterEffect(_jsonOptions);
                            using var jsonEffect = new JsonEffectProvider(
                                null, // No logger for concurrent test to reduce complexity
                                _configuration
                            );

                            var input = MemoryTestModelFactory.CreateInput(
                                id: $"Concurrent_{taskId}_{iteration}",
                                dataSizeBytes: 3000,
                                description: $"Concurrent test task {taskId} iteration {iteration}"
                            );

                            var output = new MemoryTestOutput
                            {
                                Id = input.Id,
                                ProcessedAt = DateTime.UtcNow,
                                ProcessedData = new string('C', 2000), // 2KB
                                Success = true,
                                Message = $"Concurrent output {taskId}_{iteration}"
                            };

                            var metadata = new Metadata
                            {
                                Name = $"ConcurrentMetadata_{taskId}_{iteration}",
                                InputObject = input,
                                OutputObject = output
                            };

                            await parameterEffect.Track(metadata);
                            await jsonEffect.Track(metadata);

                            await parameterEffect.SaveChanges(CancellationToken.None);
                            await jsonEffect.SaveChanges(CancellationToken.None);

                            // Small delay to simulate realistic concurrency
                            await Task.Delay(1);
                        }
                    });

                await Task.WhenAll(tasks);

                // Cleanup after concurrent execution
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            },
            "CrossComponent_Concurrent_Memory"
        );

        Console.WriteLine(result.GetSummary());

        // Concurrent cross-component execution should not cause memory issues
        result
            .MemoryRetained.Should()
            .BeLessThan(
                18 * 1024 * 1024,
                "Concurrent cross-component execution should not cause memory issues"
            );

        // Memory should be properly managed under concurrent load
        result
            .MemoryRetained.Should()
            .BeLessThan(
                5_000_000,
                "Memory should be properly managed under concurrent cross-component load"
            );
    }
}

// Mock configuration for testing
public class MockChainSharpEffectConfiguration(JsonSerializerOptions options)
    : IChainSharpEffectConfiguration
{
    public JsonSerializerOptions SystemJsonSerializerOptions { get; } = options;

    public JsonSerializerSettings NewtonsoftJsonSerializerSettings { get; } =
        new() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

    public bool SerializeStepData { get; } = true;
}

// Helper factory classes for testing
public class TestParameterEffectProviderFactory(JsonSerializerOptions options)
    : IEffectProviderFactory
{
    public ChainSharp.Effect.Services.EffectProvider.IEffectProvider Create()
    {
        return new ParameterEffect(options);
    }
}

public class TestJsonEffectProviderFactory(
    JsonSerializerOptions options,
    IChainSharpEffectConfiguration configuration
) : IEffectProviderFactory
{
    private readonly JsonSerializerOptions _options = options;

    public ChainSharp.Effect.Services.EffectProvider.IEffectProvider Create()
    {
        return new JsonEffectProvider(null, configuration);
    }
}
