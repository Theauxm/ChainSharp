using ChainSharp.Logging.Enums;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Models.Metadata.DTOs;
using ChainSharp.Logging.Postgres.Services.PostgresContextFactory;
using ChainSharp.Logging.Services.LoggedWorkflow;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using ChainSharp.Logging.Services.WorkflowLogger;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Logging.Postgres.Integration.IntegrationTests;

public class PostgresContextTests : TestSetup
{
    public override ServiceProvider ConfigureServices(IServiceCollection services) =>
        services.BuildServiceProvider();

    [Theory]
    public async Task TestPostgresProviderCanCreateMetadata()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<ILoggingProviderContextFactory>();

        var context = postgresContextFactory.Create();

        var metadata = Metadata.Create(context, new CreateMetadata() { Name = "TestMetadata" });

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
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<ILoggingProviderContextFactory>();
        var logger = new WorkflowLogger();

        var workflow = new TestWorkflow(postgresContextFactory, logger);

        // Act
        await workflow.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    }

    private class TestWorkflow(
        ILoggingProviderContextFactory contextFactory,
        IWorkflowLogger logger
    ) : LoggedWorkflow<Unit, Unit>(contextFactory, logger)
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
            Activate(input).Resolve();
    }
}
