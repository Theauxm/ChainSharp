using ChainSharp.Examples.Brewery.Steps.Bottle;
using ChainSharp.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Examples.Brewery;

internal interface ICider : IWorkflow<Ingredients, List<GlassBottle>>
{
}