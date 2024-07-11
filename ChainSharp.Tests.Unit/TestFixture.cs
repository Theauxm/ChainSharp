namespace ChainSharp.Tests.Unit;

[SetUpFixture]
public class TestFixture : ITestFixture
{
    [OneTimeSetUp]
    public async Task RunBeforeAnyTests() { }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests() { }
}
