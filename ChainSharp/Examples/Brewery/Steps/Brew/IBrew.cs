using ChainSharp.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Examples.Brewery.Steps.Brew;

internal interface IBrew : IStep<BrewingJug, Unit>
{
}