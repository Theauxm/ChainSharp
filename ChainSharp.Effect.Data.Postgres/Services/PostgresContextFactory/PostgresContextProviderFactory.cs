using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Services.EffectProvider;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;

public class PostgresContextProviderFactory(
    IDbContextFactory<PostgresContext.PostgresContext> dbContextFactory
) : IDataContextProviderFactory
{
    public int Count { get; private set; } = 0;

    public IEffectProvider Create()
    {
        var context = dbContextFactory.CreateDbContext();

        Count++;

        return context;
    }

    public async Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Count++;

        return context;
    }
}
