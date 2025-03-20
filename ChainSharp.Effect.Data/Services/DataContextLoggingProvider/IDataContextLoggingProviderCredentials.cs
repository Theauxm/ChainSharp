using ChainSharp.Effect.Data.Enums;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public interface IDataContextLoggingProviderCredentials
{
    public EvaluationStrategy EvaluationStrategy { get; }

    public LogLevel MinimumLogLevel { get; }
}

public class DataContextLoggingProviderCredentials : IDataContextLoggingProviderCredentials
{
    public EvaluationStrategy EvaluationStrategy { get; set; } = EvaluationStrategy.Lazy;

    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}
