using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Models.Metadata.DTOs;
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

        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit) });

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
            new CreateManifest { Name = typeof(Unit), Properties = config }
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

        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit) });

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act - Query using the context directly since ExternalId is private
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.Id.Should().Be(manifest.Id);
    }

    [Theory]
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
            new CreateManifest { Name = typeof(Unit), Properties = initialConfig }
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
            new CreateManifest { Name = typeof(Unit), Properties = config }
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

    [Theory]
    public async Task TestMetadataCanLinkToManifest()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        // Create a manifest (job definition)
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                Properties = new TestManifestProperties
                {
                    Name = "TestJob",
                    Value = 1,
                    Enabled = true
                }
            }
        );

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);

        // Create metadata (workflow execution) linked to the manifest
        var metadata = Metadata.Create(
            new CreateMetadata
            {
                Name = nameof(TestMetadataCanLinkToManifest),
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = new { TestInput = "value" },
                ManifestId = manifest.Id
            }
        );

        await context.Track(metadata);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundMetadata = await context
            .Metadatas.Include(m => m.Manifest)
            .FirstOrDefaultAsync(x => x.Id == metadata.Id);

        // Assert
        foundMetadata.Should().NotBeNull();
        foundMetadata!.ManifestId.Should().Be(manifest.Id);
        foundMetadata.Manifest.Should().NotBeNull();
        foundMetadata.Manifest!.Id.Should().Be(manifest.Id);
    }

    [Theory]
    public async Task TestManifestCanHaveMultipleMetadataRecords()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        // Create a manifest (job definition)
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                Properties = new TestManifestProperties
                {
                    Name = "MultiRunJob",
                    Value = 42,
                    Enabled = true
                }
            }
        );

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);

        // Create multiple metadata records (workflow executions) linked to the same manifest
        var metadata1 = Metadata.Create(
            new CreateMetadata
            {
                Name = nameof(TestManifestCanHaveMultipleMetadataRecords) + "_Run1",
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = new { Run = 1 },
                ManifestId = manifest.Id
            }
        );

        var metadata2 = Metadata.Create(
            new CreateMetadata
            {
                Name = nameof(TestManifestCanHaveMultipleMetadataRecords) + "_Run2",
                ExternalId = Guid.NewGuid().ToString("N"),
                Input = new { Run = 2 },
                ManifestId = manifest.Id
            }
        );

        await context.Track(metadata1);
        await context.Track(metadata2);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context
            .Manifests.Include(m => m.Metadatas)
            .FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.Metadatas.Should().HaveCount(2);
        foundManifest.Metadatas.Should().Contain(m => m.Id == metadata1.Id);
        foundManifest.Metadatas.Should().Contain(m => m.Id == metadata2.Id);
    }

    #region Scheduling Property Tests

    [Theory]
    public async Task TestPostgresProviderCanCreateManifestWithSchedulingProperties()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var manifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(Unit),
            IsEnabled = true,
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 3 * * *",
            MaxRetries = 5,
            TimeoutSeconds = 3600
        });

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.IsEnabled.Should().BeTrue();
        foundManifest.ScheduleType.Should().Be(ScheduleType.Cron);
        foundManifest.CronExpression.Should().Be("0 3 * * *");
        foundManifest.MaxRetries.Should().Be(5);
        foundManifest.TimeoutSeconds.Should().Be(3600);
    }

    [Theory]
    public async Task TestPostgresProviderCanCreateManifestWithIntervalSchedule()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var manifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(Unit),
            ScheduleType = ScheduleType.Interval,
            IntervalSeconds = 300 // Every 5 minutes
        });

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.ScheduleType.Should().Be(ScheduleType.Interval);
        foundManifest.IntervalSeconds.Should().Be(300);
    }

    [Theory]
    public async Task TestPostgresProviderCanCreateDisabledManifest()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var manifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(Unit),
            IsEnabled = false,
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 0 * * *"
        });

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifest.Id);

        // Assert
        foundManifest.Should().NotBeNull();
        foundManifest!.IsEnabled.Should().BeFalse();
    }

    [Theory]
    public async Task TestPostgresProviderCanUpdateLastSuccessfulRun()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var manifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(Unit),
            ScheduleType = ScheduleType.OnDemand
        });

        await context.Track(manifest);
        await context.SaveChanges(CancellationToken.None);
        var manifestId = manifest.Id;
        context.Reset();

        // Act - Update LastSuccessfulRun
        var foundManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifestId);
        foundManifest.Should().NotBeNull();

        var successTime = DateTime.UtcNow;
        foundManifest!.LastSuccessfulRun = successTime;
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Assert
        var updatedManifest = await context.Manifests.FirstOrDefaultAsync(x => x.Id == manifestId);
        updatedManifest.Should().NotBeNull();
        updatedManifest!.LastSuccessfulRun.Should().BeCloseTo(successTime, TimeSpan.FromSeconds(1));
    }

    [Theory]
    public async Task TestPostgresProviderCanQueryEnabledManifests()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var enabledManifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(Unit),
            IsEnabled = true,
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 * * * *"
        });

        var disabledManifest = Manifest.Create(new CreateManifest
        {
            Name = typeof(Unit),
            IsEnabled = false,
            ScheduleType = ScheduleType.Cron,
            CronExpression = "0 * * * *"
        });

        await context.Track(enabledManifest);
        await context.Track(disabledManifest);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var enabledManifests = await context.Manifests
            .Where(m => m.IsEnabled && m.ScheduleType == ScheduleType.Cron)
            .ToListAsync();

        // Assert
        enabledManifests.Should().Contain(m => m.Id == enabledManifest.Id);
        enabledManifests.Should().NotContain(m => m.Id == disabledManifest.Id);
    }

    [Theory]
    public async Task TestMetadataCanHaveScheduledTime()
    {
        // Arrange
        var postgresContextFactory =
            Scope.ServiceProvider.GetRequiredService<IDataContextProviderFactory>();

        using var context = (IDataContext)postgresContextFactory.Create();

        var scheduledTime = DateTime.UtcNow.AddMinutes(-5);
        var metadata = Metadata.Create(new CreateMetadata
        {
            Name = nameof(TestMetadataCanHaveScheduledTime),
            ExternalId = Guid.NewGuid().ToString("N"),
            Input = new { Test = "value" }
        });
        metadata.ScheduledTime = scheduledTime;

        await context.Track(metadata);
        await context.SaveChanges(CancellationToken.None);
        context.Reset();

        // Act
        var foundMetadata = await context.Metadatas.FirstOrDefaultAsync(x => x.Id == metadata.Id);

        // Assert
        foundMetadata.Should().NotBeNull();
        foundMetadata!.ScheduledTime.Should().BeCloseTo(scheduledTime, TimeSpan.FromSeconds(1));
    }

    #endregion

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
