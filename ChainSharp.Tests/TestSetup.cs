using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ChainSharp.Tests;

public abstract class TestSetup
{
    internal IServiceProvider ServiceProvider { get; set; }

    public abstract IServiceProvider ConfigureServices(IServiceCollection services);

    [SetUp]
    public virtual async Task TestSetUp()
    {
        ServiceProvider = ConfigureServices(new ServiceCollection());
    }

    [TearDown]
    public async Task TestTearDown() { }
}