using ChainSharp.Logging.InMemory;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Models.Metadata.DTOs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Tests.Logging.InMemory.Integration.IntegrationTests;

public class InMemoryProviderTests : TestSetup
{
    [Theory]
    public async Task TestInMemoryProviderCanCreateMetadata()
    {
        // Arrange
        var inMemoryContextFactory = new InMemoryContextFactory();

        var context = inMemoryContextFactory.Create();

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
}
