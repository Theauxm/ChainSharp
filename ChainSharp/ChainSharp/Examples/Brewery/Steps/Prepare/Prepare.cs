using ChainSharp.Exceptions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Examples.Brewery.Steps.Prepare;

internal class Prepare : Step<Ingredients, BrewingJug>, IPrepare
{
    public override async Task<Either<WorkflowException, BrewingJug>> Run(Ingredients input)
    {
        const int gallonWater = 1;

        var gallonAppleJuice = await Boil(gallonWater, input.Apples, input.BrownSugar);

        if (gallonAppleJuice.IsLeft)
            return gallonAppleJuice.Swap().ValueUnsafe();
        
        return new BrewingJug()
        {
            Gallons = gallonAppleJuice.ValueUnsafe(),
            Ingredients = input
        };
    }

    private async Task<Either<WorkflowException, int>> Boil(int gallonWater, int numApples, int ozBrownSugar)
    {
        return gallonWater + (numApples / 8) + (ozBrownSugar / 128);
    }
}