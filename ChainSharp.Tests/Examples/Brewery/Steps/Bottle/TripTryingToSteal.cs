using ChainSharp.Exceptions;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Bottle;

public class TripTryingToSteal: Step<BrewingJug, List<GlassBottle>>
{
    public override async Task<Either<WorkflowException, List<GlassBottle>>> Run(BrewingJug input)
    {
        // We try to steal the cinnamon, but we trip and fall and GO LEFT
        return new WorkflowException("You done messed up now, son.");
    }
}