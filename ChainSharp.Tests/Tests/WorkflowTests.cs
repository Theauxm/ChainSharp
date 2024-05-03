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
        services.AddScoped<IBottle, Bottle>();
        
        return services.BuildServiceProvider();
    }
    
    [SetUp]
    public override async Task TestSetUp()
    {
        await base.TestSetUp();

        _brew =
            ServiceProvider.GetRequiredService<IBrew>();
    }
    
    private class ChainTest(IBrew brew, IPrepare prepare, IBottle bottle) : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
            => Activate(input, "this is a test string to make sure it gets added to memory")
                .Chain<IPrepare, Ingredients, BrewingJug>(prepare)
                .Chain<Ferment, BrewingJug>()
                .Chain<TwoTupleStepTest, (Ingredients, BrewingJug)>()
                .Chain<ThreeTupleStepTest, (Ingredients, BrewingJug, Unit)>()
                .Chain<IBrew, BrewingJug>(brew)
                .Chain<IBottle, BrewingJug, List<GlassBottle>>(bottle)
                .Resolve();
    }
    
    private class ChainTestWithNoInputs : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
        {
            var brew = new Brew();
            var ferment = new Ferment() as IFerment;
            return Activate(input, "this is a test string to make sure it gets added to memory")
                .AddServices(ferment)
                .Chain<Prepare>()
                .Chain<Ferment>()
                .Chain<TwoTupleStepTest>()
                .Chain<ThreeTupleStepTest>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }

    private class WorkflowTestWithTupleInput : Workflow<(int, string, object), Unit>
    {
        protected override async Task<Either<Exception, Unit>> RunInternal((int, string, object) input)
            => Activate(input)
                .Chain<TupleReturnStep>()
                .ShortCircuit<TupleReturnStep>()
                .Resolve();
    }
    
    
    private class ChainTestWithUnitInput: Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
        {
            var brew = new Brew();
            var ferment = new Ferment() as IFerment;
            return Activate(input, "this is a test string to make sure it gets added to memory")
                .AddServices(ferment)
                .Chain<Meditate>()
                .Chain<Prepare>()
                .Chain<Ferment>()
                .Chain<TwoTupleStepTest>()
                .Chain<ThreeTupleStepTest>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }
    
    
    private class ChainTestWithShortCircuit(IPrepare prepare, IFerment ferment) : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
        {
            var brew = new Brew();
            return Activate(input)
                .AddServices(prepare, ferment)
                .IChain<IPrepare>()
                .Chain<Ferment>()
                .Chain<TwoTupleStepTest>()
                .Chain<ThreeTupleStepTest>()
                .ShortCircuit<StealCinnamonAndRunAway>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }

    private class ChainTestWithShortCircuitStaysLeft(IPrepare prepare, IFerment ferment) : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
        {
            var brew = new Brew();
            return Activate(input)
                .AddServices(prepare, ferment)
                .IChain<IPrepare>()
                .ShortCircuit<TripTryingToSteal>()
                .Chain<Ferment>()
                .Chain<TwoTupleStepTest>()
                .Chain<ThreeTupleStepTest>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }
    
    private class TwoTupleStepTest : Step<(Ingredients, BrewingJug), Unit>
    {
        public override async Task<Unit> Run((Ingredients, BrewingJug) input)
        {
            var (x, y) = input;

            x.Apples++;
            y.Gallons++;

            return Unit.Default;
        }
    }
    
    private class TupleReturnStep : Step<Unit, (bool, double, object)>
    {
        public override async Task<(bool, double, object)> Run(Unit input)
        {
            return (true, 1, new object());
        }
    }

    private class ThreeTupleStepTest : Step<(Ingredients, BrewingJug, Unit), Unit>
    {
        public override async Task<Unit> Run((Ingredients, BrewingJug, Unit) input)
        {
            var (x, y, z) = input;

            x.Apples++;
            y.Gallons++;

            return Unit.Default;
        }
    }

    [Theory]
    public async Task TestInputOfTuple()
    {
        // Arrange
        
        // Act
        var result = await new WorkflowTestWithTupleInput().Run((1, "hello", new object()));

        // Assert
    }
    
    [Theory]
    public async Task TestChain()
    {
        var prepare = ServiceProvider.GetRequiredService<IPrepare>();
        var bottle = ServiceProvider.GetRequiredService<IBottle>();
        
        var workflow = new ChainTest(_brew, prepare, bottle);
        
        var ingredients = new Ingredients()
        {
            Apples = 1,
            BrownSugar = 1,
            Cinnamon = 1,
            Yeast = 1
        };

        var result = await workflow.Run(ingredients);
    }
    
    [Theory]
    public async Task TestChainWithNoInputs()
    {
        var workflow = new ChainTestWithNoInputs();
        
        var ingredients = new Ingredients()
        {
            Apples = 1,
            BrownSugar = 1,
            Cinnamon = 1,
            Yeast = 1
        };

        var result = await workflow.Run(ingredients);
    }
    
    [Theory]
    public async Task TestChainWithShortCircuit()
    {
        var prepare = ServiceProvider.GetRequiredService<IPrepare>();
        var ferment = ServiceProvider.GetRequiredService<IFerment>();
        
        var workflow = new ChainTestWithShortCircuit(prepare, ferment);
        
        var ingredients = new Ingredients()
        {
            Apples = 1,
            BrownSugar = 1,
            Cinnamon = 1,
            Yeast = 1
        };
        
        var result = await workflow.Run(ingredients);
    }
    
    [Theory]
    public async Task TestChainWithUnitInput()
    {
        var prepare = ServiceProvider.GetRequiredService<IPrepare>();
        
        var workflow = new ChainTestWithUnitInput();
        
        var ingredients = new Ingredients()
        {
            Apples = 1,
            BrownSugar = 1,
            Cinnamon = 1,
            Yeast = 1
        };

        var result = await workflow.Run(ingredients);
    }
    
    [Theory]
    public async Task TestChainWithShortCircuitStaysLeft()
    {
        var prepare = ServiceProvider.GetRequiredService<IPrepare>();
        var ferment = ServiceProvider.GetRequiredService<IFerment>();
        
        var workflow = new ChainTestWithShortCircuitStaysLeft(prepare, ferment);
        
        var ingredients = new Ingredients()
        {
            Apples = 1,
            BrownSugar = 1,
            Cinnamon = 1,
            Yeast = 1
        };
        
        var result = await workflow.Run(ingredients);
    }
}