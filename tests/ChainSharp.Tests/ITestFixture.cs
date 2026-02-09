namespace ChainSharp.Tests;

public interface ITestFixture
{
    Task RunBeforeAnyTests();

    Task RunAfterAnyTests();
}
