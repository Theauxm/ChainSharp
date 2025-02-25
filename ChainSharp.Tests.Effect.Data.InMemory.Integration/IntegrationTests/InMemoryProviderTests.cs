using ChainSharp.Effect.Data.Models.Metadata;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
using ChainSharp.Effect.Services.EffectLogger;
using ChainSharp.Effect.Services.EffectWorkflow;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Data.InMemory.Integration.IntegrationTests;

public class InMemoryProviderTests : TestSetup
{
    [Theory]
    public async Task TestInMemoryProviderCanCreateMetadata()
    {
        // Arrange
        var inMemoryContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextFactory>();

        var context = inMemoryContextFactory.Create();

        var metadata = Metadata.Create(context, new CreateMetadata() { Name = "TestMetadata" });

        await context.SaveChanges();
        context.Reset();

        // Act
        var foundMetadata = await context.PersistentMetadatas.FirstOrDefaultAsync(
            x => x.Id == metadata.Id
        );

        // Assert
        foundMetadata.Should().NotBeNull();
        foundMetadata.Id.Should().Be(metadata.Id);
        foundMetadata.Name.Should().Be(metadata.Name);
    }

    [Theory]
    public async Task TestInMemoryProviderCanRunWorkflow()
    {
        // Arrange
        var inMemoryContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextFactory>();
        var logger = new EffectLogger();

        var workflow = new TestWorkflow(inMemoryContextFactory, logger);

        // Act
        await workflow.Run(Unit.Default);

        // Assert
        workflow.Metadata.Name.Should().Be("TestWorkflow");
        workflow.Metadata.FailureException.Should().BeNullOrEmpty();
        workflow.Metadata.FailureReason.Should().BeNullOrEmpty();
        workflow.Metadata.FailureStep.Should().BeNullOrEmpty();
        workflow.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    }

    private class TestWorkflow(IDataContextFactory contextFactory, IEffectLogger logger)
        : EffectWorkflow<Unit, Unit>(contextFactory, logger)
    {
        protected override async Task<Either<Exception, Unit>> RunInternal(Unit input) =>
            Activate(input).Resolve();
    }
}
