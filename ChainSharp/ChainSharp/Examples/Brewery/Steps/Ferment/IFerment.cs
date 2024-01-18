using ChainSharp.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Examples.Brewery.Steps.Ferment;

internal interface IFerment : IStep<BrewingJug, Unit>
{
}