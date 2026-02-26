using ChainSharp.Exceptions;
using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Brew;
using ChainSharp.Tests.Examples.Brewery.Steps.Ferment;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using ChainSharp.Train;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery;

public class Cider(IPrepare prepare, IFerment ferment, IBrew brew, IBottle bottle)
    : Train<Ingredients, List<GlassBottle>>,
        ICider
{
    protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
        Ingredients input
    ) =>
        Activate(input)
            .AddServices<IPrepare, IFerment, IBrew, IBottle>(prepare, ferment, brew, bottle)
            .IChain<IPrepare>()
            .IChain<IFerment>()
            .IChain<IBrew>()
            .IChain<IBottle>()
            .Resolve();
}
