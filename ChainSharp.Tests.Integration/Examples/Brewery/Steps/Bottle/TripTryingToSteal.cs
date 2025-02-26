using ChainSharp.Exceptions;
using ChainSharp.Step;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Integration.Examples.Brewery.Steps.Bottle;

public class TripTryingToSteal : Step<BrewingJug, List<GlassBottle>>
{
    public override async Task<List<GlassBottle>> Run(BrewingJug input)
    {
        // We try to steal the cinnamon, but we trip and fall and GO LEFT
        throw new WorkflowException("You done messed up now, son.");
    }
}
