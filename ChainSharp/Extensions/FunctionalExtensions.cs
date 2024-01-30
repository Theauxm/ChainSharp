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
    
    internal static R Unwrap<L, R>(this Either<L, R> option)
        where L : Exception
    {
        if (option.IsRight)
            return option.RightToSeq().Head();
        
        throw option.LeftToSeq().Head;
    }

    internal static bool IsTuple(this Type type)
        => type.IsGenericType && type.FullName.StartsWith("System.ValueTuple`");
}