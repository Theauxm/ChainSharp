namespace ChainSharp.Tests.Logging.InMemory.Integration;

public abstract class TestSetup
{
    [SetUp]
    public virtual async Task TestSetUp() { }

    [TearDown]
    public async Task TestTearDown() { }
}
