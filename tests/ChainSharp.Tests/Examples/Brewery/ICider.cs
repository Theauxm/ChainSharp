using ChainSharp.Tests.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using ChainSharp.Workflow;

namespace ChainSharp.Tests.Examples.Brewery;

public interface ICider : IWorkflow<Ingredients, List<GlassBottle>> { }
