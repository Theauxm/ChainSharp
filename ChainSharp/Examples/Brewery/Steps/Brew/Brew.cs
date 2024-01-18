using ChainSharp.Examples.Brewery.Steps.Prepare;
using ChainSharp.Exceptions;
using LanguageExt;

namespace ChainSharp.Examples.Brewery.Steps.Brew;

internal class Brew : Step<BrewingJug, Unit>, IBrew
{
    public override async Task<Either<WorkflowException, Unit>> Run(BrewingJug input)
    {
        if (!input.IsFermented)
            return new WorkflowException("We cannot brew our Cider before it is fermented!");
        
        // Pretend that we waited 2 days...
        input.IsBrewed = true;

        return Unit.Default;
    }
}