using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Examples.Brewery;

internal interface ICider : IWorkflow<Ingredients, List<GlassBottle>>
{
}