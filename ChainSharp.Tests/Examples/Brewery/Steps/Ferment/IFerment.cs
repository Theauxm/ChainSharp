using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Ferment;

public interface IFerment : IStep<BrewingJug, Unit>
{
}