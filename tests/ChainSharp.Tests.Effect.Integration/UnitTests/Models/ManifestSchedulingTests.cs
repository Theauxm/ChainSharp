using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest;
using ChainSharp.Effect.Models.Manifest.DTOs;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Effect.Integration.UnitTests.Models;

[TestFixture]
public class ManifestSchedulingTests
{
    [Test]
    public void Create_WithDefaults_ShouldHaveCorrectSchedulingDefaults()
    {
        // Arrange & Act
        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit) });

        // Assert
        manifest.IsEnabled.Should().BeTrue();
        manifest.ScheduleType.Should().Be(ScheduleType.None);
        manifest.CronExpression.Should().BeNull();
        manifest.IntervalSeconds.Should().BeNull();
        manifest.MaxRetries.Should().Be(3);
        manifest.TimeoutSeconds.Should().BeNull();
        manifest.LastSuccessfulRun.Should().BeNull();
    }

    [Test]
    public void Create_ShouldGenerateNonEmptyExternalId()
    {
        // Arrange & Act
        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit) });

        // Assert
        manifest.ExternalId.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Create_ShouldGenerateUniqueExternalIds()
    {
        // Arrange & Act
        var manifest1 = Manifest.Create(new CreateManifest { Name = typeof(Unit) });
        var manifest2 = Manifest.Create(new CreateManifest { Name = typeof(Unit) });

        // Assert
        manifest1.ExternalId.Should().NotBe(manifest2.ExternalId);
    }

    [Test]
    public void ExternalId_ShouldBeSettableToArbitraryString()
    {
        // Arrange
        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit) });
        var customId = "custom-slug-identifier-that-is-not-a-guid";

        // Act
        manifest.ExternalId = customId;

        // Assert
        manifest.ExternalId.Should().Be(customId);
    }

    [Test]
    public void Create_WithCronSchedule_ShouldSetCronProperties()
    {
        // Arrange & Act
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                ScheduleType = ScheduleType.Cron,
                CronExpression = "0 3 * * *" // Daily at 3am
            }
        );

        // Assert
        manifest.ScheduleType.Should().Be(ScheduleType.Cron);
        manifest.CronExpression.Should().Be("0 3 * * *");
        manifest.IntervalSeconds.Should().BeNull();
    }

    [Test]
    public void Create_WithIntervalSchedule_ShouldSetIntervalProperties()
    {
        // Arrange & Act
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                ScheduleType = ScheduleType.Interval,
                IntervalSeconds = 300 // Every 5 minutes
            }
        );

        // Assert
        manifest.ScheduleType.Should().Be(ScheduleType.Interval);
        manifest.IntervalSeconds.Should().Be(300);
        manifest.CronExpression.Should().BeNull();
    }

    [Test]
    public void Create_WithOnDemandSchedule_ShouldSetScheduleType()
    {
        // Arrange & Act
        var manifest = Manifest.Create(
            new CreateManifest { Name = typeof(Unit), ScheduleType = ScheduleType.OnDemand }
        );

        // Assert
        manifest.ScheduleType.Should().Be(ScheduleType.OnDemand);
    }

    [Test]
    public void Create_WithDisabled_ShouldSetIsEnabledFalse()
    {
        // Arrange & Act
        var manifest = Manifest.Create(
            new CreateManifest { Name = typeof(Unit), IsEnabled = false }
        );

        // Assert
        manifest.IsEnabled.Should().BeFalse();
    }

    [Test]
    public void Create_WithCustomMaxRetries_ShouldSetMaxRetries()
    {
        // Arrange & Act
        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit), MaxRetries = 10 });

        // Assert
        manifest.MaxRetries.Should().Be(10);
    }

    [Test]
    public void Create_WithZeroMaxRetries_ShouldAllowZero()
    {
        // Arrange & Act
        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit), MaxRetries = 0 });

        // Assert
        manifest.MaxRetries.Should().Be(0);
    }

    [Test]
    public void Create_WithTimeout_ShouldSetTimeoutSeconds()
    {
        // Arrange & Act
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                TimeoutSeconds = 3600 // 1 hour
            }
        );

        // Assert
        manifest.TimeoutSeconds.Should().Be(3600);
    }

    [Test]
    public void Create_WithAllSchedulingProperties_ShouldSetAllProperties()
    {
        // Arrange & Act
        var manifest = Manifest.Create(
            new CreateManifest
            {
                Name = typeof(Unit),
                IsEnabled = true,
                ScheduleType = ScheduleType.Cron,
                CronExpression = "0 */6 * * *", // Every 6 hours
                MaxRetries = 5,
                TimeoutSeconds = 7200
            }
        );

        // Assert
        manifest.IsEnabled.Should().BeTrue();
        manifest.ScheduleType.Should().Be(ScheduleType.Cron);
        manifest.CronExpression.Should().Be("0 */6 * * *");
        manifest.MaxRetries.Should().Be(5);
        manifest.TimeoutSeconds.Should().Be(7200);
    }

    [Test]
    public void LastSuccessfulRun_ShouldBeSettable()
    {
        // Arrange
        var manifest = Manifest.Create(new CreateManifest { Name = typeof(Unit) });
        var successTime = DateTime.UtcNow;

        // Act
        manifest.LastSuccessfulRun = successTime;

        // Assert
        manifest.LastSuccessfulRun.Should().Be(successTime);
    }

    [Test]
    public void ScheduleType_Enum_ShouldHaveCorrectValues()
    {
        // Assert enum values are as expected
        ((int)ScheduleType.None)
            .Should()
            .Be(0);
        ((int)ScheduleType.Cron).Should().Be(1);
        ((int)ScheduleType.Interval).Should().Be(2);
        ((int)ScheduleType.OnDemand).Should().Be(3);
    }
}
