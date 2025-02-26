using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Services.EffectWorkflow;
using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Json.Integration.IntegrationTests;

public class JsonEffectProviderTests : TestSetup
{
    public override ServiceProvider ConfigureServices(IServiceCollection services) =>
        services.AddScopedChainSharpWorkflow<ITestWorkflow, TestWorkflow>().BuildServiceProvider();

    [Theory]
    public async Task TestJsonEffect()
    {
        // Arrange
        var workflow = Scope.ServiceProvider.GetRequiredService<ITestWorkflow>();
        var workflowTwo = Scope.ServiceProvider.GetRequiredService<ITestWorkflow>();

        // Act
        await workflow.Run(Unit.Default);
        await workflowTwo.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    }

    private class TestWorkflow : EffectWorkflow<Unit, Unit>, ITestWorkflow
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
            Activate(input).Resolve();
    }

    private interface ITestWorkflow : IEffectWorkflow<Unit, Unit> { }
}
