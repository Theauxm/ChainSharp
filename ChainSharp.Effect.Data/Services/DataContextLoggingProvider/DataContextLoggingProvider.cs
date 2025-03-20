using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLoggingProvider(
    IDataContextProviderFactory dataContextProviderFactory,
    IDataContextLoggingProviderCredentials credentials
) : IDataContextLoggingProvider
{
    private List<DataContextLogger> Contexts { get; } = [];

    public void Dispose()
    {
        foreach (var context in Contexts)
            context.DataContext.Dispose();
    }

    public ILogger CreateLogger(string categoryName)
    {
        var context = (IDataContext)dataContextProviderFactory.Create();

        var logger = new DataContextLogger(
            context,
            credentials.EvaluationStrategy,
            categoryName,
            credentials.MinimumLogLevel
        );

        Contexts.Add(logger);

        return logger;
    }
}
