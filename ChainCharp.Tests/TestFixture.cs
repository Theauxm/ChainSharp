using NUnit.Framework;

namespace ChainCharp.Tests;


[SetUpFixture]
public class TestFixture
{
    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
    }
}