using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Ferment;

internal interface IFerment : IStep<BrewingJug, Unit>
{
}