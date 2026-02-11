using System.Text.RegularExpressions;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Log.DTOs;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLogger(
    IDataContextProviderFactory dbContextFactory,
    string categoryName,
    LogLevel minimumLogLevel,
    HashSet<string> exactBlacklist,
    List<Regex> wildcardBlacklist
) : ILogger
{
    public async void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (categoryName == "Microsoft.EntityFrameworkCore.Database.Command")
            return;

        if (logLevel < minimumLogLevel || IsBlacklisted(categoryName))
            return;

        var log = Effect.Models.Log.Log.Create(
            new CreateLog
            {
                Level = logLevel,
                Message = formatter(state, exception),
                CategoryName = categoryName,
                EventId = eventId.Id,
                Exception = exception
            }
        );

        using var dataContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        await dataContext.Track(log);
        await dataContext.SaveChanges(CancellationToken.None);
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLogLevel;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    private bool IsBlacklisted(string category) =>
        exactBlacklist.Contains(category)
        || wildcardBlacklist.Any(regex => regex.IsMatch(category));
}
