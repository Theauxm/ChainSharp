using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Services.EffectProvider;
using ChainSharp.Effect.Services.EffectProviderFactory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;

public class PostgresContextProviderFactory(NpgsqlDataSource dataSource)
    : IDataContextProviderFactory
{
    public int Count { get; private set; } = 0;

    public IEffectProvider Create()
    {
        var context = new PostgresContext.PostgresContext(
            new DbContextOptionsBuilder<PostgresContext.PostgresContext>()
                .UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention()
                .Options
        );

        Count++;

        return context;
    }
}
