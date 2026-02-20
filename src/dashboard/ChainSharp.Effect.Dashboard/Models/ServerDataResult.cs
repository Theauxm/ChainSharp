namespace ChainSharp.Effect.Dashboard.Models;

public record ServerDataResult<T>(IEnumerable<T> Items, int TotalCount);
