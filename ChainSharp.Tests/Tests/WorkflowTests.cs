using ChainSharp.Exceptions;
using ChainSharp.Step;
using ChainSharp.Tests.Examples.Brewery;
using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Brew;
using ChainSharp.Tests.Examples.Brewery.Steps.Ferment;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using ChainSharp.Workflow;
using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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

        _brew = ServiceProvider.GetRequiredService<IBrew>();
    }

    private class ChainTest(IBrew brew, IPrepare prepare, IBottle bottle)
        : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        ) =>
            Activate(input, "this is a test string to make sure it gets added to memory")
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
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
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

    private class ChainTestWithInterfaceTuple : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
        {
            var brew = new Brew();
            var ferment = new Ferment() as IFerment;
            return Activate(input, "this is a test string to make sure it gets added to memory")
                .AddServices(ferment)
                .Chain<PrepareWithInterface>()
                .Chain<TwoTupleStepInterfaceTest>() // What we're really testing here
                .Chain<CastBrewingJug>()
                .Chain<Ferment>()
                .Chain<ThreeTupleStepTest>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }

    private class ChainTestWithOneTypedService : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
        {
            var brew = new Brew();
            var ferment = new Ferment();

            // IFerment implements IStep and IFerment
            // Normally, AddServices looks for the First Interface that is not IStep
            // This uses a Type argument to do a Service addition to find IFerment
            // (which is actually the second interface that it implements)

            return Activate(input, "this is a test string to make sure it gets added to memory")
                .AddServices<IFerment>(ferment)
                .Chain<Prepare>()
                .IChain<IFerment>()
                .Chain<TwoTupleStepTest>()
                .Chain<ThreeTupleStepTest>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }

    private class ChainTestWithTwoTypedServices : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
        {
            var brew = new Brew();
            var ferment = new Ferment();
            var prepare = new Prepare(ferment);

            // IFerment implements IStep and IFerment
            // Normally, AddServices looks for the First Interface that is not IStep
            // This uses a Type argument to do a Service addition to find IFerment
            // (which is actually the second interface that it implements)

            return Activate(input, "this is a test string to make sure it gets added to memory")
                .AddServices<IPrepare, IFerment>(prepare, ferment)
                .IChain<IPrepare>()
                .IChain<IFerment>()
                .Chain<TwoTupleStepTest>()
                .Chain<ThreeTupleStepTest>()
                .Chain(brew)
                .Chain<Bottle>()
                .Resolve();
        }
    }

    private class ChainTestWithMockedService : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
        {
            var brew = new Brew();
            var ferment = new Mock<IFerment>().Object;
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

    private class WorkflowTestWithTupleInput
        : Workflow<(int, string, object), (bool, double, object)>
    {
        protected override async Task<Either<Exception, (bool, double, object)>> RunInternal(
            (int, string, object) input
        ) => Activate(input).Chain<TupleReturnStep>().ShortCircuit<TupleReturnStep>().Resolve();
    }

    private class ChainTestWithUnitInput : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
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

    private class ChainTestWithShortCircuit(IPrepare prepare, IFerment ferment)
        : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
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

    private class ChainTestWithShortCircuitStaysLeft(IPrepare prepare, IFerment ferment)
        : Workflow<Ingredients, List<GlassBottle>>
    {
        protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
            Ingredients input
        )
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

    private class OuterProperty
    {
        public string OuterString { get; set; }

        public InnerProperty InnerProperty { get; set; }
    }

    private class InnerProperty
    {
        public int Number { get; set; }
    }

    private class OuterField
    {
        public string OuterString;

        public InnerField InnerProperty;
    }

    private class InnerField
    {
        public int Number;
    }

    private class AccessInnerPropertyTypeWorkflow : Workflow<OuterProperty, InnerProperty>
    {
        protected override async Task<Either<Exception, InnerProperty>> RunInternal(
            OuterProperty input
        ) => Activate(input).Extract<OuterProperty, InnerProperty>().Resolve();
    }

    private class AccessInnerFieldTypeWorkflow : Workflow<OuterField, InnerField>
    {
        protected override async Task<Either<Exception, InnerField>> RunInternal(
            OuterField input
        ) => Activate(input).Extract<OuterField, InnerField>().Resolve();
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

    /// <summary>
    /// Tests to ensure that tuple casting to interface works correctly
    /// </summary>
    private class TwoTupleStepInterfaceTest : Step<(Ingredients, IBrewingJug), Unit>
    {
        public override async Task<Unit> Run((Ingredients, IBrewingJug) input)
        {
            var (x, y) = input;

            x.Apples++;
            y.Gallons++;

            return Unit.Default;
        }
    }

    private class CastBrewingJug : Step<IBrewingJug, BrewingJug>
    {
        public override async Task<BrewingJug> Run(IBrewingJug input)
        {
            // MAGIC!
            return (BrewingJug)input;
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
    public async Task TestExtract()
    {
        // Arrange
        var outerProperty = new OuterProperty()
        {
            OuterString = "hello world",
            InnerProperty = new InnerProperty() { Number = 7 }
        };

        var outerField = new OuterField()
        {
            OuterString = "hello mars",
            InnerProperty = new InnerField() { Number = 8 }
        };

        // Act
        var propertyResult = await new AccessInnerPropertyTypeWorkflow().Run(outerProperty);
        var fieldResult = await new AccessInnerFieldTypeWorkflow().Run(outerField);

        // Assert
        propertyResult.Number.Should().Be(7);
        fieldResult.Number.Should().Be(8);
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
    public async Task TestChainWithMockedService()
    {
        var workflow = new ChainTestWithMockedService();

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
    public async Task TestChainWithInterfaceTupleArgument()
    {
        var workflow = new ChainTestWithInterfaceTuple();

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
    public async Task TestChainWithOneTypedService()
    {
        var workflow = new ChainTestWithOneTypedService();

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
    public async Task TestChainWithTwoTypedService()
    {
        var workflow = new ChainTestWithTwoTypedServices();

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
