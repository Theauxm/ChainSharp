using ChainSharp.Exceptions;
using ChainSharp.Step;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Ferment;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

namespace ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;

public class PrepareWithInterface(IFerment ferment) : Step<Ingredients, IBrewingJug>
{
    public override async Task<IBrewingJug> Run(Ingredients input)
    {
        const int gallonWater = 1;

        var gallonAppleJuice = await Boil(gallonWater, input.Apples, input.BrownSugar);

        if (gallonAppleJuice.IsLeft)
            throw gallonAppleJuice.Swap().ValueUnsafe();

        return new BrewingJug() { Gallons = gallonAppleJuice.ValueUnsafe(), Ingredients = input };
    }

    private async Task<Either<WorkflowException, int>> Boil(
        int gallonWater,
        int numApples,
        int ozBrownSugar
    )
    {
        return gallonWater + (numApples / 8) + (ozBrownSugar / 128);
    }
}
