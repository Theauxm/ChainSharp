using ChainSharp.Effect.Configuration.ChainSharpEffectBuilder;
using ChainSharp.Effect.Orchestration.Scheduler.Extensions;
using ChainSharp.Effect.Orchestration.Scheduler.Services.Scheduling;
using ChainSharp.Tests.Effect.Dashboard.Integration.Fakes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Dashboard.Integration;

[TestFixture]
public class CyclicDependencyValidationTests
{
    private IServiceCollection _services = null!;
    private ChainSharpEffectConfigurationBuilder _parentBuilder = null!;

    [SetUp]
    public void SetUp()
    {
        _services = new ServiceCollection();
        _parentBuilder = new ChainSharpEffectConfigurationBuilder(_services);
    }

    #region Valid DAGs (no cycles)

    [Test]
    public void Build_NoDependencies_Succeeds()
    {
        // Arrange & Act
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler.Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                        "job-a",
                        new FakeManifestInputA(),
                        Every.Minutes(5)
                    )
            );

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Build_LinearChainDifferentGroups_Succeeds()
    {
        // Arrange: group-a → group-b → group-c
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler
                        .Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "job-a",
                            new FakeManifestInputA(),
                            Every.Minutes(5),
                            options => options.Group("group-a")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowB, FakeManifestInputB>(
                            "job-b",
                            new FakeManifestInputB(),
                            options => options.Group("group-b")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowC, FakeManifestInputC>(
                            "job-c",
                            new FakeManifestInputC(),
                            options => options.Group("group-c")
                        )
            );

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Build_WithinGroupDependency_Succeeds()
    {
        // Arrange: Both jobs in the same group — same-group edges should not trigger validation
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler
                        .Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "job-a",
                            new FakeManifestInputA(),
                            Every.Minutes(5),
                            options => options.Group("same-group")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowB, FakeManifestInputB>(
                            "job-b",
                            new FakeManifestInputB(),
                            options => options.Group("same-group")
                        )
            );

        // Assert
        act.Should().NotThrow();
    }

    [Test]
    public void Build_DiamondDagDifferentGroups_Succeeds()
    {
        // Arrange: group-a → group-b, group-a → group-c (via separate chains)
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler
                        .Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "job-a",
                            new FakeManifestInputA(),
                            Every.Minutes(5),
                            options => options.Group("group-a")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowB, FakeManifestInputB>(
                            "job-b",
                            new FakeManifestInputB(),
                            options => options.Group("group-b")
                        )
                        .Schedule<IFakeSchedulerWorkflowC, FakeManifestInputC>(
                            "job-c",
                            new FakeManifestInputC(),
                            Every.Minutes(5),
                            options => options.Group("group-a")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowD, FakeManifestInputD>(
                            "job-d",
                            new FakeManifestInputD(),
                            options => options.Group("group-c")
                        )
            );

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Cyclic Dependencies (should throw)

    [Test]
    public void Build_TwoGroupCycle_ThrowsInvalidOperationException()
    {
        // Arrange: group-a → group-b and group-b → group-a
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler
                        // Chain 1: group-a → group-b
                        .Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "job-a",
                            new FakeManifestInputA(),
                            Every.Minutes(5),
                            options => options.Group("group-a")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowB, FakeManifestInputB>(
                            "job-b",
                            new FakeManifestInputB(),
                            options => options.Group("group-b")
                        )
                        // Chain 2: group-b → group-a (creates cycle)
                        .Schedule<IFakeSchedulerWorkflowC, FakeManifestInputC>(
                            "job-c",
                            new FakeManifestInputC(),
                            Every.Minutes(5),
                            options => options.Group("group-b")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowD, FakeManifestInputD>(
                            "job-d",
                            new FakeManifestInputD(),
                            options => options.Group("group-a")
                        )
            );

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*manifest groups*");
    }

    [Test]
    public void Build_ThreeGroupCycle_ThrowsAndListsCycleMembers()
    {
        // Arrange: group-a → group-b → group-c → group-a
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler
                        .Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "job-a",
                            new FakeManifestInputA(),
                            Every.Minutes(5),
                            options => options.Group("group-a")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowB, FakeManifestInputB>(
                            "job-b",
                            new FakeManifestInputB(),
                            options => options.Group("group-b")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowC, FakeManifestInputC>(
                            "job-c",
                            new FakeManifestInputC(),
                            options => options.Group("group-c")
                        )
                        // Close the cycle: group-c → group-a
                        .Schedule<IFakeSchedulerWorkflowD, FakeManifestInputD>(
                            "job-d",
                            new FakeManifestInputD(),
                            Every.Minutes(5),
                            options => options.Group("group-c")
                        )
                        .ThenInclude<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "job-a2",
                            new FakeManifestInputA(),
                            options => options.Group("group-a")
                        )
            );

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Circular dependency*")
            .WithMessage("*group-a*")
            .WithMessage("*group-b*")
            .WithMessage("*group-c*");
    }

    [Test]
    public void Build_CycleWithDefaultGroupIds_ThrowsInvalidOperationException()
    {
        // Arrange: When groupId is null, externalId becomes the group.
        // "ext-a" → "ext-b" and "ext-b" → "ext-a" (cycle via default group IDs)
        var act = () =>
            _parentBuilder.AddScheduler(
                scheduler =>
                    scheduler
                        .Schedule<IFakeSchedulerWorkflowA, FakeManifestInputA>(
                            "ext-a",
                            new FakeManifestInputA(),
                            Every.Minutes(5)
                        )
                        .ThenInclude<IFakeSchedulerWorkflowB, FakeManifestInputB>(
                            "ext-b",
                            new FakeManifestInputB()
                        )
                        .Schedule<IFakeSchedulerWorkflowC, FakeManifestInputC>(
                            "ext-b",
                            new FakeManifestInputC(),
                            Every.Minutes(5)
                        )
                        .ThenInclude<IFakeSchedulerWorkflowD, FakeManifestInputD>(
                            "ext-a",
                            new FakeManifestInputD()
                        )
            );

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Circular dependency*");
    }

    #endregion
}
