using ChainSharp.Exceptions;
using ChainSharp.Step;
using ChainSharp.Tests.Examples.Brewery.Steps.Ferment;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Bottle;

public class Bottle(IFerment ferment) : Step<BrewingJug, List<GlassBottle>>, IBottle
{
    public override async Task<List<GlassBottle>> Run(BrewingJug input)
    {
        if (!input.IsBrewed)
            throw new WorkflowException("We don't want to bottle un-brewed beer! What are we, trying to make poison?");

        // 16 oz bottles
        var bottlesNeeded = input.Gallons / 8;

        var filledBottles = new List<GlassBottle>();
        for (var i = 0; i < bottlesNeeded; i++)
        {
            filledBottles.Add(new GlassBottle()
            {
                HasCider = true
            });
        }

        return filledBottles;
    }
}