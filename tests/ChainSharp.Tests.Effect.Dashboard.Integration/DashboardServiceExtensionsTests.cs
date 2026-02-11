using ChainSharp.Effect.Dashboard.Configuration;
using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Dashboard.Integration;

[TestFixture]
public class DashboardServiceExtensionsTests
{
    [Test]
    public void AddChainSharpDashboard_RegistersDashboardOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChainSharpDashboard();
        using var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetService<DashboardOptions>();
        options.Should().NotBeNull();
        options!.RoutePrefix.Should().Be("/chainsharp");
        options.Title.Should().Be("ChainSharp");
    }

    [Test]
    public void AddChainSharpDashboard_WithConfigure_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChainSharpDashboard(o =>
        {
            o.Title = "Custom Title";
            o.RoutePrefix = "/custom";
        });
        using var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<DashboardOptions>();
        options.Title.Should().Be("Custom Title");
        options.RoutePrefix.Should().Be("/custom");
    }

    [Test]
    public void AddChainSharpDashboard_RegistersIServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChainSharpDashboard();
        using var provider = services.BuildServiceProvider();

        // Assert — WorkflowDiscoveryService depends on IServiceCollection being resolvable
        var serviceCollection = provider.GetService<IServiceCollection>();
        serviceCollection.Should().NotBeNull();
        serviceCollection.Should().BeSameAs(services);
    }

    [Test]
    public void AddChainSharpDashboard_RegistersWorkflowDiscoveryService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChainSharpDashboard();
        using var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var discoveryService = scope.ServiceProvider.GetService<IWorkflowDiscoveryService>();
        discoveryService.Should().NotBeNull();
        discoveryService.Should().BeOfType<WorkflowDiscoveryService>();
    }
}
