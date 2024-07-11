using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Workflow;

public class ActivateTests : TestSetup
{
    [Theory]
    public async Task TestActivateTests()
    {
        // Arrange
        var input = new object();
        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Count.Should().Be(2);
        workflow.Memory.Should().ContainValue(input);
        workflow.Exception.Should().BeNull();
    }

    [Theory]
    public async Task TestInvalidActivateTests()
    {
        // Arrange
        object input = null!;
        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().NotBeNull();
    }

    [Theory]
    public async Task TestActivateTestsParamsInput()
    {
        // Arrange
        var input = new object();
        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input, 1, "hello", false);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Count.Should().Be(5);
        workflow.Memory.Should().ContainValue(input);
        workflow.Memory.Should().ContainValue(1);
        workflow.Memory.Should().ContainValue(false);
        workflow.Memory.Should().ContainValue("hello");
        workflow.Exception.Should().BeNull();
    }

    [Theory]
    public async Task TestActivateTestsTupleInput()
    {
        // Arrange
        var inputObject = new object();
        var input = (1, inputObject);
        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input, 2, "hello", false);

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Count.Should().Be(5);
        workflow.Memory.Should().ContainValue(input);
        workflow.Memory.Should().ContainValue(2);
        workflow.Memory.Should().ContainValue(false);
        workflow.Memory.Should().ContainValue("hello");
        workflow.Exception.Should().BeNull();
    }

    [Theory]
    public async Task TestActivateTestsParamsTupleInput()
    {
        // Arrange
        var input = new object();
        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input, (1, "hello", false));

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Count.Should().Be(5);
        workflow.Memory.Should().ContainValue(input);
        workflow.Memory.Should().ContainValue(1);
        workflow.Memory.Should().ContainValue(false);
        workflow.Memory.Should().ContainValue("hello");
        workflow.Exception.Should().BeNull();
    }

    public class TestWorkflow : Workflow<object, string>
    {
        protected override Task<Either<Exception, string>> RunInternal(object input) =>
            throw new NotImplementedException();
    }

    public class TestTupleWorkflow : Workflow<(int, object), string>
    {
        protected override Task<Either<Exception, string>> RunInternal((int, object) input) =>
            throw new NotImplementedException();
    }
}
