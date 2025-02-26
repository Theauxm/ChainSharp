using ChainSharp.Step;
using ChainSharp.Tests.Integration.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Integration.Examples.Brewery.Steps.Ferment;

public interface IFerment : IStep<BrewingJug, Unit> { }
