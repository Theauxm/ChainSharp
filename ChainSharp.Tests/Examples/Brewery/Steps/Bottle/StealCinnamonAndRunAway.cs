using ChainSharp.Exceptions;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Bottle;

internal class StealCinnamonAndRunAway: Step<BrewingJug, List<GlassBottle>>
{
    public override async Task<Either<WorkflowException, List<GlassBottle>>> Run(BrewingJug input)
    {
        // We steal the Cinnamon Sticks and make a run for it with some empty bottles
        input.Ingredients.Cinnamon = 0;
        input.HasCinnamonSticks = false;
        var emptyBottles = new List<GlassBottle>()
        {
            new() {},
            new() {},
            new() {},
        };
        return emptyBottles;
    }
}