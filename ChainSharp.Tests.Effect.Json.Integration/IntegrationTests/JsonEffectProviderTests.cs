using ChainSharp.ArrayLogger.Services.ArrayLoggingProvider;
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
        var arrayProvider = Scope.ServiceProvider.GetRequiredService<IArrayLoggingProvider>();

        // Act
        await workflow.Run(Unit.Default);
        await workflowTwo.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
        arrayProvider.Loggers.Should().NotBeNullOrEmpty();
        arrayProvider.Loggers.Should().HaveCount(3);
        
        // Verify that we have the expected logger types:
        // 1. Two workflow loggers (ILogger<EffectWorkflow<Unit, Unit>>) - may have empty logs
        // 2. One JsonEffectProvider logger (ILogger<JsonEffectProvider>) - should have JSON logs
        var jsonProviderLoggers = arrayProvider.Loggers
            .Where(logger => logger.Logs.Any(log => 
                log.Category == "ChainSharp.Effect.Json.Services.JsonEffect.JsonEffectProvider"))
            .ToList();
        
        jsonProviderLoggers.Should().HaveCount(1, 
            "There should be exactly one JsonEffectProvider logger with JSON metadata logs");
        
        var jsonProviderLogger = jsonProviderLoggers.First();
        jsonProviderLogger.Logs.Should().NotBeEmpty(
            "JsonEffectProvider logger should contain JSON metadata logs");
    }

    private class TestWorkflow : EffectWorkflow<Unit, Unit>, ITestWorkflow
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
            Activate(input).Resolve();
    }

    private interface ITestWorkflow : IEffectWorkflow<Unit, Unit> { }
}
