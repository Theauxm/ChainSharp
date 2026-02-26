using ChainSharp.Route;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;

namespace ChainSharp.Tests.Integration.Examples.Brewery;

public interface ICider : IRoute<Ingredients, List<GlassBottle>> { }
