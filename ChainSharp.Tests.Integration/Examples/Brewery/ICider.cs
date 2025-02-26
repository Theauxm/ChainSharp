using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Bottle;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;
using ChainSharp.Workflow;

namespace ChainSharp.Tests.Integration.Examples.Brewery;

public interface ICider : IWorkflow<Ingredients, List<GlassBottle>> { }
