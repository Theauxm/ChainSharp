using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Bottle;

internal interface IBottle : IStep<BrewingJug, List<GlassBottle>>
{
}