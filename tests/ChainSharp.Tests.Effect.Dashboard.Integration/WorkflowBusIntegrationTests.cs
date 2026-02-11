using ChainSharp.Effect.Dashboard.Extensions;
using ChainSharp.Effect.Dashboard.Services.WorkflowDiscovery;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Mediator.Extensions;
using ChainSharp.Tests.Effect.Dashboard.Integration.Fakes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Dashboard.Integration;

[TestFixture]
public class WorkflowBusIntegrationTests
{
    [Test]
    public void DiscoverWorkflows_WithEffectWorkflowBus_FindsAssemblyScannedWorkflows()
    {
        // Arrange — register workflows via assembly scanning (as in a real app)
        var services = new ServiceCollection();

        services.AddChainSharpEffects(
            o => o.AddEffectWorkflowBus(assemblies: [typeof(FakeWorkflowA).Assembly])
        );

        services.AddChainSharpDashboard();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var discoveryService =
            scope.ServiceProvider.GetRequiredService<IWorkflowDiscoveryService>();

        // Act
        var result = discoveryService.DiscoverWorkflows();

        // Assert — fake workflows from this test assembly should be discovered
        result.Should().Contain(r => r.InputType == typeof(FakeInputA));
        result.Should().Contain(r => r.InputType == typeof(FakeInputB));
        result.Should().Contain(r => r.InputType == typeof(FakeInputC));
        result.Should().Contain(r => r.InputType == typeof(List<string>));
    }
}
