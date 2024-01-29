using ChainSharp.Exceptions;
using ChainSharp.Tests.Examples.Brewery;
using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Brew;
using ChainSharp.Tests.Examples.Brewery.Steps.Ferment;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ChainSharp.Tests.Tests;

public class WorkflowTests : TestSetup
{
    private IBrew _brew;
    
    public override IServiceProvider ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICider, Cider>();
        services.AddScoped<IPrepare, Prepare>();
        services.AddScoped<IBrew, Brew>();
        services.AddScoped<IFerment, Ferment>();
        
        return services.BuildServiceProvider();
    }
    
    [SetUp]
    public override async Task TestSetUp()
    {
        await base.TestSetUp();

        _brew =
            ServiceProvider.GetRequiredService<IBrew>();
    }
    
    private class ChainTest(IBrew brew) : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<WorkflowException, List<GlassBottle>>> RunInternal(Ingredients input)
            => Activate(input)
                .Chain<Prepare, Ingredients, BrewingJug>()
                .Chain<Ferment, BrewingJug>()
                .Chain<IBrew, BrewingJug>(brew)
                .Chain<Bottle, BrewingJug, List<GlassBottle>>()
                .Resolve();
    }
    
    [Theory]
    public async Task TestChain()
    {
        var workflow = new ChainTest(_brew);

        var result = await workflow.Run(new Ingredients()
        {
            Apples = 1,
            BrownSugar = 1,
            Cinnamon = 1,
            Yeast = 1
        });
    }
}