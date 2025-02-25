using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Services.EffectFactory;

namespace ChainSharp.Effect.Data.Services.IDataContextFactory;

/// <summary>
/// Factory for each LoggingProviderContext.
/// Each provider will likely implement this on their own.
/// </summary>
public interface IDataContextFactory : IEffectFactory
{
    public new IDataContext Create();
}
