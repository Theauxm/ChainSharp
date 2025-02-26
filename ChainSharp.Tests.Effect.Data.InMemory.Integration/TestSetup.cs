using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.InMemory.Extensions;
using ChainSharp.Effect.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ChainSharp.Tests.Effect.Data.InMemory.Integration;

public abstract class TestSetup
{
    private ServiceProvider ServiceProvider { get; set; }

    public IServiceScope Scope { get; private set; }

    private ServiceCollection ServiceCollection { get; set; }

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        ServiceCollection = new ServiceCollection();

        ServiceCollection.AddChainSharpEffects(
            options => options.AddInMemoryEffect().AddConsoleLogger()
        );

        ServiceProvider = ConfigureServices(ServiceCollection);
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

    public abstract ServiceProvider ConfigureServices(IServiceCollection services);
}
