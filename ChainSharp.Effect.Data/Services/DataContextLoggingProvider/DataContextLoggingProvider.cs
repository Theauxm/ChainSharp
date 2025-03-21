using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public class DataContextLoggingProvider(
    IDataContextProviderFactory dataContextProviderFactory,
    IDataContextLoggingProviderCredentials credentials
) : IDataContextLoggingProvider
{
    public void Dispose() { }

    public ILogger CreateLogger(string categoryName)
    {
        return new DataContextLogger(
            dataContextProviderFactory,
            credentials.EvaluationStrategy,
            categoryName,
            credentials.MinimumLogLevel
        );
    }
}
