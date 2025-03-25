using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.InMemory.Services.InMemoryContextFactory;

/// <summary>
/// InMemory Provider Context Factory implementation.
/// </summary>
public class InMemoryContextProviderFactory : IDataContextProviderFactory
{
    public IDataContext Create() =>
        new InMemoryContext.InMemoryContext(
            new DbContextOptionsBuilder<InMemoryContext.InMemoryContext>()
                .UseInMemoryDatabase("InMemoryDb")
                .Options
        );

    public async Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return Create();
    }

    IEffectProvider IEffectProviderFactory.Create() => Create();
}
