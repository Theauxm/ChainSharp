using ChainSharp.Exceptions;
using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Brew;
using ChainSharp.Tests.Examples.Brewery.Steps.Ferment;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery;

public class Cider(
    IPrepare prepare,
    IFerment ferment,
    IBrew brew,
    IBottle bottle) : Workflow<Ingredients, List<GlassBottle>>, ICider
{
    protected override async Task<Either<WorkflowException, List<GlassBottle>>> RunInternal(Ingredients input)
        => this
            .Chain<IPrepare, Ingredients, BrewingJug>(prepare, input, out var jug)
            .Chain<IFerment, BrewingJug>(ferment, jug)
            .Chain<IBrew, BrewingJug>(brew, jug)
            .Chain<IBottle, BrewingJug, List<GlassBottle>>(bottle, jug, out var bottles)
            .Resolve(bottles);
}