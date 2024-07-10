using ChainSharp.Step;
using ChainSharp.Tests.Examples.Brewery.Steps.Prepare;
using LanguageExt;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Brew;

public interface IBrew : IStep<BrewingJug, Unit>
{
}