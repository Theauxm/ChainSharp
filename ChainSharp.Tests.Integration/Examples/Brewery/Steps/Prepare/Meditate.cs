using ChainSharp.Exceptions;
using ChainSharp.Step;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using static LanguageExt.Prelude;

namespace ChainSharp.Tests.Examples.Brewery.Steps.Prepare;

internal class Meditate : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        // You silently consider what you should brew
        return unit;
    }
}
