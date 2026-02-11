using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ChainSharp.Effect.Data.Extensions;

public static class FunctionalExtensions
{
    public static void AssertLoaded<T>(
        [NotNull] this T? value,
        [CallerArgumentExpression("value")] string? valueExpr = null
    )
    {
        if (value == null)
            throw new InvalidOperationException($"{valueExpr} has not been loaded");
    }

    public static void AssertEachLoaded<T, U>(
        [NotNull] this IEnumerable<T> values,
        Func<T, U> selector,
        [CallerArgumentExpression("values")] string? valuesExpr = null,
        [CallerArgumentExpression("selector")] string? selectorExpr = null
    )
    {
        foreach (var value in values)
        {
            if (selector(value) == null)
                throw new InvalidOperationException(
                    $"{valuesExpr}({selectorExpr}) has not been loaded"
                );
        }
    }
}
