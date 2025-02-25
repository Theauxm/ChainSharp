using ChainSharp.Effect.Data.Services.DataContext;
using ChainSharp.Effect.Data.Services.IDataContextFactory;
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
}
