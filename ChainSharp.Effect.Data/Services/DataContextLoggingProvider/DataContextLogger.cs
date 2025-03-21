using ChainSharp.Effect.Data.Enums;
using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Log;
using ChainSharp.Effect.Models.Log.DTOs;
using ChainSharp.Effect.Services.EffectProvider;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLogger(
    IDataContextProviderFactory dataContextProvider,
    EvaluationStrategy evaluationStrategy,
    string categoryName,
    LogLevel minimumLogLevel
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
        if (minimumLogLevel > logLevel)
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

        var dataContext = (IDataContext)dataContextProvider.Create();
        dataContext.Track(log);
        dataContext.SaveChanges().Wait();
        dataContext.Dispose();
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;
}
