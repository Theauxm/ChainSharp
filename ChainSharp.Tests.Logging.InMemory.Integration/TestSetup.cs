using ChainSharp.Logging.Extensions;
using ChainSharp.Logging.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Logging.InMemory.Integration;

public abstract class TestSetup
{
    private ServiceProvider ServiceProvider { get; set; }

    public IServiceScope Scope { get; private set; }

    private ServiceCollection ServiceCollection { get; set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        ServiceCollection = new ServiceCollection();

        ServiceProvider = ServiceCollection
            .AddChainSharpLogging(options => options.UseInMemoryProvider().AddConsoleLogger())
            .BuildServiceProvider();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await ServiceProvider.DisposeAsync();
    }

    [SetUp]
    public virtual async Task TestSetUp()
    {
        Scope = ServiceProvider.CreateScope();
    }

    [TearDown]
    public async Task TestTearDown()
    {
        Scope.Dispose();
    }
}
