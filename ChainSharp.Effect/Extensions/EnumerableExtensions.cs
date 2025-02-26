using LanguageExt;

namespace ChainSharp.Effect.Extensions;

public static class EnumerableExtensions
{
    public static void RunAll<T>(this IEnumerable<T> source, Action<T> action)
    {
        source.Aggregate(
            (Action)(() => { }), // Initial empty action (does nothing)
            (acc, item) =>
            {
                acc(); // Execute previous action
                return () => action(item); // Return new action for next iteration
            }
        )();
    }

    public static List<TResult> RunAll<T, TResult>(
        this IEnumerable<T> source,
        Func<T, TResult> func
    )
    {
        return source.Aggregate(
            new List<TResult>(), // Initial accumulator
            (acc, item) =>
            {
                acc.Add(func(item)); // Apply function and store result
                return acc; // Return updated list
            }
        );
    }

    public static async Task<List<TResult>> RunAllAsync<T, TResult>(
        this IEnumerable<T> source,
        Func<T, Task<TResult>> func
    )
    {
        return await source.Aggregate(
            Task.FromResult(new List<TResult>()),
            async (accTask, item) =>
            {
                var acc = await accTask; // Await the accumulated list
                var result = await func(item); // Execute the async function
                acc.Add(result); // Store the result
                return acc; // Return updated list
            }
        );
    }

    public static async Task RunAllAsync<T>(this IEnumerable<T> source, Func<T, Task> func)
    {
        await source.Aggregate(
            Task.CompletedTask, // Initial accumulator (empty task)
            async (acc, item) =>
            {
                await acc; // Ensure previous task is completed before starting the next
                await func(item); // Execute the function
            }
        );
    }
}
