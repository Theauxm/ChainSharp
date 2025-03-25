using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Services.EffectProviderFactory;

namespace ChainSharp.Effect.Data.Services.IDataContextFactory;

/// <summary>
/// Factory for each LoggingProviderContext.
/// Each provider will likely implement this on their own.
/// </summary>
public interface IDataContextProviderFactory : IEffectProviderFactory
{
    public Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken);
}
