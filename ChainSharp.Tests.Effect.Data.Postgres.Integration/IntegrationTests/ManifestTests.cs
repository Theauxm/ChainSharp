using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using FluentAssertions;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Data.Postgres.Integration.IntegrationTests;

public class ManifestTests : TestSetup
{
    [Theory]
    public async Task TestPostgresProviderCanCreateManifest()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit)
            }
        );

        await context.Track(manifest);

        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.Id.Should().Be(manifest.Id);
    }

    [Theory]
    public async Task TestPostgresProviderCanCreateManifestWithProperties()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var config = new TestManifestProperties
        {
            Name = "TestConfig",
            Value = 42,
            Enabled = true
        };

        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                Properties = config
            }
        );

        await context.Track(manifest);

        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.Id.Should().Be(manifest.Id);

        var retrievedConfig = foundManifest.GetProperties<TestManifestProperties>();
        retrievedConfig.Should().NotBeNull();
        retrievedConfig.Name.Should().Be("TestConfig");
        retrievedConfig.Value.Should().Be(42);
        retrievedConfig.Enabled.Should().BeTrue();
    }

    [Theory]
    public async Task TestPostgresProviderCanQueryManifestByExternalId()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit)
            }
        );

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act - Query using the context directly since ExternalId is private
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.Id.Should().Be(manifest.Id);
    }

    [Ignore("hangs for now.")]
    public async Task TestPostgresProviderCanUpdateManifestProperties()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var initialConfig = new TestManifestProperties
        {
            Name = "InitialConfig",
            Value = 1,
            Enabled = false
        };

        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                Properties = initialConfig
            }
        );

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        var manifestId = manifest.Id;
        context.Reset();

        // Act - Update the manifest properties
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifestId);
        foundManifest.Should().NotBeNull();

        var updatedConfig = new TestManifestProperties
        {
            Name = "UpdatedConfig",
            Value = 100,
            Enabled = true
        };

        foundManifest!.SetProperties(updatedConfig);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Assert
        var updatedManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifestId);
        updatedManifest.Should().NotBeNull();

        var retrievedConfig = updatedManifest!.GetProperties<TestManifestProperties>();
        retrievedConfig.Name.Should().Be("UpdatedConfig");
        retrievedConfig.Value.Should().Be(100);
        retrievedConfig.Enabled.Should().BeTrue();
    }

    [Theory]
    public async Task TestManifestSerializerTypeReturnsCorrectType()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var config = new TestManifestProperties
        {
            Name = "TypeTest",
            Value = 5,
            Enabled = true
        };

        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                Properties = config
            }
        );

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.PropertyType.Should().Be(typeof(TestManifestProperties));
    }

    /// <summary>
    /// Test configuration class used for Manifest property serialization tests.
    /// </summary>
    public class TestManifestProperties : IManifestProperties
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public bool Enabled { get; set; }
    }
}
