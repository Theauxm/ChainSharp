using ChainSharp.Effect.Data.Enums;

namespace ChainSharp.Effect.Data.Services.DataContextLoggingProvider;

public interface IDataContextLoggingProviderCredentials
{
    public EvaluationStrategy EvaluationStrategy { get; }
}

public class DataContextLoggingProviderCredentials : IDataContextLoggingProviderCredentials
{
    public EvaluationStrategy EvaluationStrategy { get; set; } = EvaluationStrategy.Lazy;
}
