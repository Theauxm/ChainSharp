namespace ChainSharp.Tests.Logging.InMemory.Integration;

[SetUpFixture]
public class TestFixture
{
    [OneTimeSetUp]
    public async Task RunBeforeAnyTests() { }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests() { }
}
