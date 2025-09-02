using ChainSharp.Effect.Data.InMemory.Extensions;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Json.Extensions;
using ChainSharp.Effect.Mediator.Extensions;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Effect.Parameter.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Tests.MemoryLeak.Integration;

/// <summary>
/// Test setup for memory leak integration tests.
/// Provides configured service collections for testing memory-related functionality.
/// </summary>
public static class TestSetup
{
    /// <summary>
    /// Creates a service collection configured for memory leak testing.
    /// Uses in-memory data context to avoid external dependencies while testing memory behavior.
    /// </summary>
    /// <returns>Configured service collection for memory leak tests</returns>
    public static IServiceCollection CreateMemoryLeakTestServices()
    {
        var services = new ServiceCollection();

        // Add logging with console output for test diagnostics
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add ChainSharp Effect services with all providers
        services.AddChainSharpEffects(options =>
        {
            // Use InMemory data context for tests (no external database dependencies)
            options.AddInMemoryEffect();

            // Add JSON effect provider for serialization testing
            options.AddJsonEffect();

            // Add parameter effect provider
            options.SaveWorkflowParameters();

            // Add workflow bus and mediator for testing workflow execution
            options.AddEffectWorkflowBus(assemblies: [typeof(AssemblyMarker).Assembly]);
        });

        return services;
    }

    /// <summary>
    /// Creates a service provider from the memory leak test services.
    /// </summary>
    /// <returns>Service provider for memory leak tests</returns>
    public static IServiceProvider CreateMemoryLeakTestServiceProvider()
    {
        return CreateMemoryLeakTestServices().BuildServiceProvider();
    }

    /// <summary>
    /// Creates a service collection with minimal providers for focused testing.
    /// Excludes data persistence to avoid EF Core JsonDocument serialization issues.
    /// </summary>
    /// <returns>Service collection with minimal configuration</returns>
    public static IServiceCollection CreateMinimalTestServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Less verbose for performance tests
        });

        // Configure ChainSharp without data persistence to focus on memory testing
        services.AddChainSharpEffects(options =>
        {
            // No data context - focus purely on memory leak testing
            options.AddEffectWorkflowBus(assemblies: [typeof(AssemblyMarker).Assembly]);
        });

        return services;
    }

    /// <summary>
    /// Creates a service provider optimized for memory leak testing without data persistence.
    /// </summary>
    /// <returns>Service provider for memory-focused tests</returns>
    public static IServiceProvider CreateMemoryOnlyTestServiceProvider()
    {
        return CreateMinimalTestServices().BuildServiceProvider();
    }

    /// <summary>
    /// Creates a service collection configured to test provider disposal scenarios.
    /// Includes providers that can be configured to fail during disposal.
    /// </summary>
    /// <returns>Service collection for disposal testing</returns>
    public static IServiceCollection CreateDisposalTestServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddChainSharpEffects(options =>
        {
            options.AddInMemoryEffect();
            options.AddJsonEffect();
            options.SaveWorkflowParameters();
        });

        return services;
    }

    /// <summary>
    /// Performs cleanup operations to prevent memory leaks between tests.
    /// This method should be called in test teardown methods.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Clears the static WorkflowBus method cache
    /// 2. Forces garbage collection to ensure cleanup
    /// 3. Provides a centralized cleanup point for all memory leak tests
    /// </remarks>
    public static void CleanupMemoryLeakTests()
    {
        try
        {
            // Clear the static cache that can cause memory leaks
            WorkflowBus.ClearMethodCache();

            // Force garbage collection to ensure cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch (Exception ex)
        {
            // Log cleanup failures but don't throw - we're in cleanup
            Console.WriteLine($"Warning: Failed to cleanup memory leak tests: {ex.Message}");
        }
    }
}
