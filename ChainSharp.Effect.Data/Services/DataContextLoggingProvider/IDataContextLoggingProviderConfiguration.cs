using ChainSharp.Effect.Data.Enums;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public interface IDataContextLoggingProviderConfiguration
{
    public LogLevel MinimumLogLevel { get; }

    public List<string> Blacklist { get; }
}

public class DataContextLoggingProviderConfiguration : IDataContextLoggingProviderConfiguration
{
    public EvaluationStrategy EvaluationStrategy { get; set; } = EvaluationStrategy.Lazy;

    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    public List<string> Blacklist { get; set; } = [];
}
