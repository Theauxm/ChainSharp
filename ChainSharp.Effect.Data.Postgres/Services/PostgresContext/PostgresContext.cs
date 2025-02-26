using ChainSharp.Effect.Data.Postgres.Extensions;
using ChainSharp.Effect.Data.Services.DataContext;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Postgres.Services.PostgresContext;

/// <summary>
///
/// </summary>
/// <param name="options"></param>
public class PostgresContext(DbContextOptions<PostgresContext> options)
    : DataContext<PostgresContext>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("chain_sharp").AddPostgresEnums().ApplyUtcDateTimeConverter();
    }
}
