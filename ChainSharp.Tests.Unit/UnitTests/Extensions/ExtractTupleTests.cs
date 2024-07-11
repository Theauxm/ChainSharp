using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Extensions;

public class ExtractTupleTests : TestSetup
{
    [Theory]
    public async Task TestExtractTuple()
    {
        // Arrange
        var input = 1;

        var inputObject = new object();
        var workflow = new TestWorkflow().Activate(input, "hello", false, 'c', inputObject);

        var inputTuple = typeof(ValueTuple<int, string, bool, char, object>);

        // Act
        var result = (ValueTuple<int, string, bool, char, object>)workflow.ExtractTuple(inputTuple);

        // Assert
        result.Should().NotBeNull();
        result.Item1.Should().Be(1);
        result.Item2.Should().Be("hello");
        result.Item3.Should().Be(false);
        result.Item4.Should().Be('c');
        result.Item5.Should().Be(inputObject);
    }

    [Theory]
    public async Task TestInvalidExtractTuple()
    {
        // Arrange
        var input = 1;

        var workflow = new TestWorkflow().Activate(input);

        var inputTuple = typeof(ValueTuple<int>);

        // Act
        Assert.Throws<WorkflowException>(() => workflow.ExtractTuple(inputTuple));
    }

    private class TestWorkflow : Workflow<int, string>
    {
        protected override async Task<Either<Exception, string>> RunInternal(int input) =>
            Resolve();
    }
}
