using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Services.Effect;
using ChainSharp.Effect.Services.EffectFactory;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.InMemory.Services.InMemoryContextFactory;

/// <summary>
/// InMemory Provider Context Factory implementation.
/// </summary>
public class InMemoryContextFactory : IDataContextFactory
{
    public IDataContext Create() =>
        new InMemoryContext.InMemoryContext(
            new DbContextOptionsBuilder<InMemoryContext.InMemoryContext>()
                .UseInMemoryDatabase("InMemoryDb")
                .Options
        );

    IEffect IEffectFactory.Create() => Create();
}
