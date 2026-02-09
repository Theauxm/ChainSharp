using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Workflow;

public class ExtractTests : TestSetup
{
    [Theory]
    public async Task TestExtract()
    {
        // Arrange
        var input = 1;
        var inputObject = new TestClass() { TestString = "hello" };

        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input, inputObject).Extract<TestClass, string>();

        // Assert
        workflow.Exception.Should().BeNull();
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Should().ContainValue("hello");
    }

    [Theory]
    public async Task TestExtractTypeInput()
    {
        // Arrange
        var input = 1;
        var inputObject = new TestClass() { TestString = "hello" };

        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input).Extract<TestClass, string>(inputObject);

        // Assert
        workflow.Exception.Should().BeNull();
        workflow.Memory.Should().NotBeNull();
        workflow.Memory.Should().ContainValue("hello");
    }

    [Theory]
    public async Task TestInvalidExtract()
    {
        // Arrange
        var input = 1;
        var inputObject = new TestClass() { TestString = "hello" };

        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input, inputObject).Extract<TestClass, bool>();

        // Assert
        workflow.Exception.Should().NotBeNull();
        workflow.Memory.Should().NotBeNull();
    }

    [Theory]
    public async Task TestInvalidExtractNotInMemory()
    {
        // Arrange
        var input = 1;

        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input).Extract<TestClass, bool>();

        // Assert
        workflow.Exception.Should().NotBeNull();
        workflow.Memory.Should().NotBeNull();
    }

    [Theory]
    public async Task TestInvalidExtractTypeInputNull()
    {
        // Arrange
        var input = 1;
        var workflow = new TestWorkflow();

        // Act
        workflow.Activate(input).Extract<TestClass, string>(null!);

        // Assert
        workflow.Exception.Should().NotBeNull();
        workflow.Memory.Should().NotBeNull();
    }

    private class TestClass
    {
        public string TestString { get; set; }
    }

    private class TestWorkflow : Workflow<int, string>
    {
        protected override Task<Either<Exception, string>> RunInternal(int input) =>
            throw new NotImplementedException();
    }
}
