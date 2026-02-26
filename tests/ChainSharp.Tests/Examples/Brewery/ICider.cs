using ChainSharp.Route;
using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Examples.Brewery;

public interface ICider : IRoute<Ingredients, List<GlassBottle>> { }
