namespace ChainSharp.Tests;

public interface ITestSetup
{
    Task TestSetUp();

    Task TestTearDown();
}
