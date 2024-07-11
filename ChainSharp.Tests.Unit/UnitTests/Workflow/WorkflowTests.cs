using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Workflow;

public class WorkflowTests
{
    [Theory]
    public async Task TestUnitWorkflow()
    {
        // Arrange
        var workflow = new UnitWorkflow();

        // Act
        var result = await workflow.Run(LanguageExt.Unit.Default);

        // Assert
        result.Should().Be(LanguageExt.Unit.Default);
    }

    [Theory]
    public async Task TestInvalidWorkflow()
    {
        // Arrange
        var workflow = new NotImplementedWorkflow();

        // Act
        Assert.ThrowsAsync<NotImplementedException>(
            async () => await workflow.Run(LanguageExt.Unit.Default)
        );
    }

    private class UnitWorkflow : Workflow<LanguageExt.Unit, LanguageExt.Unit>
    {
        protected override async Task<Either<Exception, LanguageExt.Unit>> RunInternal(
            LanguageExt.Unit input
        ) => Activate(input).Resolve();
    }

    private class NotImplementedWorkflow : Workflow<LanguageExt.Unit, LanguageExt.Unit>
    {
        protected override async Task<Either<Exception, LanguageExt.Unit>> RunInternal(
            LanguageExt.Unit input
        ) => new NotImplementedException();
    }
}
