using ChainSharp.Extensions;
using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests;

public class ExtractTypeFromMemoryTests : TestSetup
{
    [Theory]
    public async Task TestExtractTypeFromMemory()
    {
        // Arrange
        var input = 1;

        var workflow = new TestWorkflow().Activate(input);

        // Act
        var result = workflow.ExtractTypeFromMemory<int, int, string>();

        // Assert
        result.Should().Be(input);
        workflow.Exception.Should().BeNull();
    }

    [Theory]
    public async Task TestTupleExtractTypeFromMemory()
    {
        // Arrange
        var input = 1;
        var tupleInput = ("hello", false);

        var workflow = new TestWorkflow().Activate(input, tupleInput);

        // Act
        var result = workflow.ExtractTypeFromMemory<(string, bool), int, string>();

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(tupleInput);
        workflow.Exception.Should().BeNull();
    }

    [Theory]
    public async Task TestTupleExtractTypeFromMemoryTypeInput()
    {
        // Arrange
        var input = 1;
        var tupleInput = ("hello", false);

        var workflow = new TestWorkflow().Activate(input, tupleInput);

        // Act
        var result = ((string, bool)?)workflow.ExtractTypeFromMemory(typeof((string, bool)));

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(tupleInput);
        workflow.Exception.Should().BeNull();
    }

    [Theory]
    public async Task TestInvalidTupleExtractTypeFromMemoryTypeInput()
    {
        // Arrange
        var input = 1;
        var tupleInput = ValueTuple.Create(1);

        var workflow = new TestWorkflow().Activate(input, tupleInput);

        // Act
        var result = (ValueTuple<int>?)workflow.ExtractTypeFromMemory(typeof(ValueTuple<int>));

        // Assert
        workflow.Exception.Should().NotBeNull();
        result.Should().BeNull();
    }

    [Theory]
    public async Task TestInvalidTupleExtractTypeFromMemory()
    {
        // Arrange
        var input = 1;
        var tupleInput = ValueTuple.Create(1);

        var workflow = new TestWorkflow().Activate(input, tupleInput);

        // Act
        var result = workflow.ExtractTypeFromMemory<ValueTuple<int>?, int, string>();

        // Assert
        workflow.Exception.Should().NotBeNull();
        result.Should().BeNull();
    }

    private class TestWorkflow : Workflow<int, string>
    {
        protected override async Task<Either<Exception, string>> RunInternal(int input) =>
            Resolve();
    }
}
