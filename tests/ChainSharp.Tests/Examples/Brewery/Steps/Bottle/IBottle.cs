using ChainSharp.Step;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Bottle;

public interface IBottle : IStep<BrewingJug, List<GlassBottle>> { }
