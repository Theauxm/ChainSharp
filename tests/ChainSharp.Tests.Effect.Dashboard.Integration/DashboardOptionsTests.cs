using ChainSharp.Effect.Dashboard.Configuration;
using FluentAssertions;

namespace ChainSharp.Tests.Effect.Dashboard.Integration;

[TestFixture]
public class DashboardOptionsTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new DashboardOptions();

        // Assert
        options.RoutePrefix.Should().Be("/chainsharp");
        options.Title.Should().Be("ChainSharp");
    }
}
