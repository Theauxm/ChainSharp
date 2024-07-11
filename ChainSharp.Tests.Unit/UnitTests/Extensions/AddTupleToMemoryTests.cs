using System.Runtime.CompilerServices;
using ChainSharp.Exceptions;
using ChainSharp.Extensions;
using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Extensions;

public class AddTupleToMemoryTests : TestSetup
{
    [Theory]
    public async Task TestAddTupleToMemory()
    {
        // Arrange
        var input = 1;
        var inputTuple = ("hello", false);

        var workflow = new TestWorkflow().Activate(input);

        // Act
        workflow.AddTupleToMemory(inputTuple);

        // Assert

        workflow.Exception.Should().BeNull();
        workflow.Memory.Count.Should().Be(4); // Unit is always added, along with input.
        workflow.Memory.Values.Should().Contain(false);
        workflow.Memory.Values.Should().Contain("hello");
    }

    [Theory]
    public async Task TestInvalidAddTupleToMemoryNotTuple()
    {
        // Arrange
        var input = 1;
        var inputTuple = "hello";

        var workflow = new TestWorkflow().Activate(input);

        // Act
        Assert.Throws<WorkflowException>(() => workflow.AddTupleToMemory(inputTuple));
    }

    [Theory]
    public async Task TestInvalidAddTupleToMemoryNull()
    {
        // Arrange
        var input = 1;
        var workflow = new TestWorkflow().Activate(input);

        // Act
        Assert.Throws<WorkflowException>(() => workflow.AddTupleToMemory((ITuple)null!));
    }

    private class TestWorkflow : Workflow<int, string>
    {
        protected override async Task<Either<Exception, string>> RunInternal(int input) =>
            Resolve();
    }
}
