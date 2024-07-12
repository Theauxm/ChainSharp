using ChainSharp.Step;
using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Tests.Unit.UnitTests.Workflow;

public class ShortCircuitTests : TestSetup
{
    [Theory]
    public async Task TestShortCircuitChain()
    {
        // Arrange
        var testStep = new TestStep();
        var inputString = "hello";
        var workflow = new TestWorkflow();

        // Act
        workflow.ShortCircuitChain<TestStep, string, bool>(
            testStep,
            inputString,
            out var returnValue
        );

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        returnValue.IsRight.Should().BeTrue();
        returnValue.ValueUnsafe().Should().BeTrue();
        workflow.Memory.Should().ContainValue(inputString.Equals("hello"));
    }

    [Theory]
    public async Task TestShortCircuitChainTupleOutput()
    {
        // Arrange
        var input = 1;
        var testStep = new TestTupleOutputStep();
        var inputString = "hello";
        var workflow = new TestWorkflow().Activate(input);

        // Act
        workflow.ShortCircuitChain<TestTupleOutputStep, string, (bool, char)>(
            testStep,
            inputString,
            out var returnValue
        );

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        returnValue.IsRight.Should().BeTrue();
        returnValue.ValueUnsafe().Should().Be((inputString.Equals("hello"), inputString.First()));
        workflow.Memory.Should().ContainValue(inputString.Equals("hello"));
        workflow.Memory.Should().ContainValue(inputString.First());
    }

    [Theory]
    public async Task TestInvalidShortCircuitChain()
    {
        // Arrange
        var input = 1;
        var testStep = new TestStep();
        var workflow = new TestWorkflow().Activate(input);

        // Act
        workflow.ShortCircuitChain<TestStep, string, bool>(
            testStep,
            new Exception(),
            out var returnValue
        );

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        returnValue.IsLeft.Should().BeTrue();
        returnValue.Swap().ValueUnsafe().Should().BeOfType<Exception>();
    }

    [Theory]
    public async Task TestShortCircuitChainFailure()
    {
        // Arrange
        var input = 1;
        var testStep = new TestExceptionStep();
        var inputString = "hello";
        var workflow = new TestWorkflow().Activate(input);

        // Act
        workflow.ShortCircuitChain<TestExceptionStep, string, bool>(
            testStep,
            inputString,
            out var returnValue
        );

        // Assert
        workflow.Memory.Should().NotBeNull();
        workflow.Exception.Should().BeNull();
        returnValue.IsLeft.Should().BeTrue();
        returnValue.Swap().ValueUnsafe().Should().BeOfType<NotImplementedException>();
        workflow.Memory.Should().NotContainValue(inputString.Equals("hello"));
    }

    private class TestExceptionStep : Step<string, bool>
    {
        public override Task<bool> Run(string input) => throw new NotImplementedException();
    }

    private class TestTupleOutputStep : Step<string, (bool, char)>
    {
        public override async Task<(bool, char)> Run(string input) =>
            (input.Equals("hello"), input.First());
    }

    private class TestStep : Step<string, bool>
    {
        public override async Task<bool> Run(string input) => input.Equals("hello");
    }

    private class TestWorkflow : Workflow<int, string>
    {
        protected override Task<Either<Exception, string>> RunInternal(int input) =>
            throw new NotImplementedException();
    }
}
