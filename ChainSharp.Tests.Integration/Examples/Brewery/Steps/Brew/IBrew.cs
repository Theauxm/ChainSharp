using ChainSharp.Step;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Integration.Examples.Brewery.Steps.Brew;

public interface IBrew : IStep<BrewingJug, Unit> { }
