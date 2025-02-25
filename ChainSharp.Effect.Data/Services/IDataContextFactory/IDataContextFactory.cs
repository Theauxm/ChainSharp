using ChainSharp.Effect.Data.Services.DataContext;

namespace ChainSharp.Effect.Data.Services.IDataContextFactory;

/// <summary>
/// Factory for each LoggingProviderContext.
/// Each provider will likely implement this on their own.
/// </summary>
public interface IDataContextFactory
{
    public IDataContext Create();
}
