using ChainSharp.Extensions;
using ChainSharp.Monad;
using ChainSharp.Step;
using ChainSharp.Train;
using FluentAssertions;
using LanguageExt;

namespace ChainSharp.Tests.Unit.UnitTests.Extensions;

public class InitializeStepTests : TestSetup
{
    [Theory]
    public async Task TestInitializeStep()
    {
        // Arrange
        var input = 1;

        var workflow = new TestWorkflow();
        var monad = workflow.Activate(1);

        // Act
        var step = monad.InitializeStep<TestValidStep, int, string>();

        // Assert
        step.Should().NotBeNull();

        var result = await step!.Run(input);
        result.Should().Be(input.ToString());
    }

    [Theory]
    public async Task TestInvalidInitializeStep()
    {
        // Arrange
        var workflow = new TestWorkflow();
        var monad = workflow.Activate(1);

        // Act
        var step = monad.InitializeStep<TestInvalidStep, int, string>();

        // Assert
        monad.Exception.Should().NotBeNull();
    }

    private class TestWorkflow : Train<int, string>
    {
        protected override async Task<Either<Exception, string>> RunInternal(int input) =>
            Activate(input).Resolve();
    }

    private class TestValidStep : Step<int, string>
    {
        public override async Task<string> Run(int input)
        {
            return input.ToString();
        }
    }

    private class TestInvalidStep(int _intInput, string _stringInput) { }
}
