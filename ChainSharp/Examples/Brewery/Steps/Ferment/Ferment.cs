using ChainSharp.Examples.Brewery.Steps.Prepare;
using ChainSharp.Exceptions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Examples.Brewery.Steps.Ferment;

internal class Ferment : Step<BrewingJug, Unit>, IFerment
{
    public override async Task<Either<WorkflowException, Unit>> Run(BrewingJug input)
    {
        var cinnamonSticks = await AddCinnamonSticks(input);

        if (cinnamonSticks.IsLeft)
            return cinnamonSticks.Swap().ValueUnsafe();

        var yeast = await AddYeast(input);

        if (yeast.IsLeft)
            return yeast.Swap().ValueUnsafe();

        input.IsFermented = true;

        return Unit.Default;
    }

    public async Task<Either<WorkflowException, Unit>> AddCinnamonSticks(BrewingJug jug)
    {
        jug.HasCinnamonSticks = jug.Ingredients.Cinnamon > 0;

        return Unit.Default;
    }
    
    public async Task<Either<WorkflowException, Unit>> AddYeast(BrewingJug jug)
    {
        if (jug.Ingredients.Yeast <= 0)
            return new WorkflowException("We need yeast to make Cider!");

        jug.Yeast = jug.Ingredients.Yeast;

        return Unit.Default;
    }
}