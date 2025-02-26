using ChainSharp.Step;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Integration.Examples.Brewery.Steps.Bottle;

public interface IBottle : IStep<BrewingJug, List<GlassBottle>> { }
