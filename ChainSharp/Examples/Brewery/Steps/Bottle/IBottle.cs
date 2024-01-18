using ChainSharp.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Examples.Brewery.Steps.Bottle;

internal interface IBottle : IStep<BrewingJug, List<GlassBottle>>
{
}