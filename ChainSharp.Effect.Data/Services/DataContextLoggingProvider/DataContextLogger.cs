using System.Text.RegularExpressions;
using ChainSharp.Effect.Data.Enums;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models.Log;
using ChainSharp.Effect.Models.Log.DTOs;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLogger(
    IDataContextProviderFactory dataContextProvider,
    EvaluationStrategy evaluationStrategy,
    LogLevel minimumLogLevel,
    HashSet<string> exactBlacklist,
    List<Regex> wildcardBlacklist,
    string categoryName
) : ILogger
{
    public List<Log> Logs { get; } = [];

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (minimumLogLevel > logLevel || IsBlacklisted(categoryName))
            return;

        var message = formatter(state, exception);

        var log = Effect.Models.Log.Log.Create(
            new CreateLog
            {
                Level = logLevel,
                Message = message,
                CategoryName = categoryName,
                EventId = eventId.Id,
                Exception = exception
            }
        );

        Logs.Add(log);

        using var dataContext = (IDataContext)dataContextProvider.Create();
        dataContext.Track(log);
        dataContext.SaveChanges().Wait();
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    private bool IsBlacklisted(string category) =>
        exactBlacklist.Contains(category)
        || wildcardBlacklist.Any(regex => regex.IsMatch(category));
}
