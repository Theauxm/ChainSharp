using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Extensions;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Services.EffectWorkflow;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Metadata = ChainSharp.Effect.Models.Metadata.Metadata;

namespace ChainSharp.Tests.Effect.Data.Postgres.Integration.IntegrationTests;

public class PostgresContextTests : TestSetup
{
    public override ServiceProvider ConfigureServices(IServiceCollection services) =>
        services.AddScopedChainSharpWorkflow<ITestWorkflow, TestWorkflow>().BuildServiceProvider();

    [Theory]
    public async Task TestPostgresProviderCanCreateMetadata()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextFactory>();

        var context = postgresContextFactory.Create();

        var metadata = Metadata.Create(new CreateMetadata() { Name = "TestMetadata" });

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

    private class TestWorkflow : EffectWorkflow<Unit, Unit>, ITestWorkflow
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
            Activate(input).Resolve();
    }

    private interface ITestWorkflow : IEffectWorkflow<Unit, Unit> { }
}
