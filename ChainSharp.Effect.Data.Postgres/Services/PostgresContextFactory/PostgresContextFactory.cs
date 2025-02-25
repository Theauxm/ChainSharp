using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
using ChainSharp.Effect.Services.Effect;
using ChainSharp.Effect.Services.EffectFactory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Services.PostgresContextFactory;

public class PostgresContextFactory(NpgsqlDataSource dataSource) : IDataContextFactory
{
    public IDataContext Create() =>
        new PostgresContext.PostgresContext(
            new DbContextOptionsBuilder<PostgresContext.PostgresContext>()
                .UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention()
                .Options
        );

    IEffect IEffectFactory.Create() => Create();
}
