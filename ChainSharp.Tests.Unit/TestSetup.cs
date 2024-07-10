namespace ChainSharp.Tests.Unit;

public abstract class TestSetup : ITestSetup
{
    [SetUp]
    public virtual async Task TestSetUp() { }

    [TearDown]
    public async Task TestTearDown() { }
}
