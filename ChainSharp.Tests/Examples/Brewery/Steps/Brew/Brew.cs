using ChainSharp.Exceptions;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Brew;

public class Brew : Step<BrewingJug, Unit>, IBrew
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