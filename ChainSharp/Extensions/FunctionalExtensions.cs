using LanguageExt;

namespace ChainSharp.Extensions;

public static class FunctionalExtensions
{
    internal static async Task<R> Unwrap<L, R>(this Task<Either<L, R>> option)
        where L : Exception
    {
        var result = await option;
        if (result.IsRight)
            return result.RightToSeq().Head();
        else
            throw result.LeftToSeq().Head;
    }
}