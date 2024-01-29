using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Brew;

internal interface IBrew : IStep<BrewingJug, Unit>
{
}