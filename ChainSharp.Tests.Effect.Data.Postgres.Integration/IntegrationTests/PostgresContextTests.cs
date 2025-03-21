using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Services.ArrayLogger;
using ChainSharp.Effect.Services.EffectWorkflow;
using ChainSharp.Step;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Metadata = ChainSharp.Effect.Models.Metadata.Metadata;

namespace ChainSharp.Tests.Effect.Data.Postgres.Integration.IntegrationTests;

public class PostgresContextTests : TestSetup
{
    public override ServiceProvider ConfigureServices(IServiceCollection services) =>
        services
            .AddTransientChainSharpWorkflow<ITestWorkflow, TestWorkflow>()
            .AddTransientChainSharpWorkflow<
                ITestWorkflowWithinWorkflow,
                TestWorkflowWithinWorkflow
            >()
            .BuildServiceProvider();

    [Theory]
    public async Task TestPostgresProviderCanCreateMetadata()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        var context = (IDataContext)postgresContextFactory.Create();

        var metadata = Metadata.Create(new CreateMetadata { Name = "TestMetadata" });

        await context.Track(metadata);

        await context.SaveChanges();
        context.Reset();

        // Act
        var foundMetadata = await context.Metadatas.FirstOrDefaultAsync(x => x.Id == metadata.Id);

        // Assert
        foundMetadata.Should().NotBeNull();
        foundMetadata.Id.Should().Be(metadata.Id);
        foundMetadata.Name.Should().Be(metadata.Name);
    }

    [Theory]
    public async Task TestPostgresProviderCanRunWorkflow()
    {
        // Arrange
        var workflow = Scope.ServiceProvider.GetRequiredService<ITestWorkflow>();

        // Act
        await workflow.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    }

    [Theory]
    public async Task TestPostgresProviderCanRunWorkflowTwo()
    {
        // Arrange
        var workflow = Scope.ServiceProvider.GetRequiredService<ITestWorkflow>();

        // Act
        await workflow.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    }

    [Theory]
    public async Task TestPostgresProviderCanRunWorkflowWithinWorkflow()
    {
        // Arrange
        var dataContextProvider =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();
        var workflow = Scope.ServiceProvider.GetRequiredService<ITestWorkflowWithinWorkflow>();
        var arrayLoggerProvider = Scope.ServiceProvider.GetRequiredService<IArrayLoggingProvider>();

        // Act
        var innerWorkflow = await workflow.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflowWithinWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
        innerWorkflow.Metadata.Name.Should().Be("TestWorkflow");
        innerWorkflow.Metadata.FailureException.Should().BeNullOrEmpty();
        innerWorkflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        innerWorkflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        innerWorkflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);

        var dataContext = (IDataContext)dataContextProvider.Create();

        var parentWorkflowResult = await dataContext.Metadatas.FirstOrDefaultAsync(
            x => x.Id == workflow.Metadata.Id
        );
        var childWorkflowResult = await dataContext.Metadatas.FirstOrDefaultAsync(
            x => x.Id == innerWorkflow.Metadata.Id
        );
        parentWorkflowResult.Should().NotBeNull();
        parentWorkflowResult!.Id.Should().Be(workflow.Metadata.Id);
        parentWorkflowResult!.WorkflowState.Should().Be(WorkflowState.Completed);

        childWorkflowResult.Should().NotBeNull();
        childWorkflowResult!.Id.Should().Be(innerWorkflow.Metadata.Id);
        childWorkflowResult!.WorkflowState.Should().Be(WorkflowState.Completed);

        var logLevel = arrayLoggerProvider
            .Loggers.SelectMany(x => x.Logs)
            .Select(x => x.Level)
            .Count(x => x == LogLevel.Critical);
        logLevel.Should().Be(1);
    }

    private class TestWorkflow : EffectWorkflow<Unit, Unit>, ITestWorkflow
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
            Activate(input).Resolve();
    }

    private class TestWorkflowWithinWorkflow()
        : EffectWorkflow<Unit, ITestWorkflow>,
            ITestWorkflowWithinWorkflow
    {
        protected override async Task<Either<Exception, ITestWorkflow>> RunInternal(Unit input) =>
            Activate(input).Chain<StepToRunTestWorkflow>().Resolve();
    }

    private class StepToRunTestWorkflow(
        ITestWorkflow testWorkflow,
        ILogger<StepToRunTestWorkflow> logger
    ) : Step<Unit, ITestWorkflow>
    {
        public override async Task<ITestWorkflow> Run(Unit input)
        {
            await testWorkflow.Run(Unit.Default);

            logger.LogCritical("Ran TestWorkflow");

            return testWorkflow;
        }
    }

    private interface ITestWorkflow : IEffectWorkflow<Unit, Unit> { }

    private interface ITestWorkflowThree : IEffectWorkflow<Unit, Unit> { }

    private interface ITestWorkflowWithinWorkflow : IEffectWorkflow<Unit, ITestWorkflow> { }
}
