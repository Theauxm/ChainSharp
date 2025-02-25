using ChainSharp.Logging.Postgres.Extensions;
using ChainSharp.Logging.Services.LoggingProviderContext;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging.Postgres.Services.PostgresContext;

/// <summary>
///
/// </summary>
/// <param name="options"></param>
public class PostgresContext(DbContextOptions<PostgresContext> options)
    : LoggingProviderContext<PostgresContext>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .HasDefaultSchema("chain_sharp")
            .AddPostgresEnums()
            .ApplyEntityOnModelCreating()
            .ApplyUtcDateTimeConverter();
    }
}
