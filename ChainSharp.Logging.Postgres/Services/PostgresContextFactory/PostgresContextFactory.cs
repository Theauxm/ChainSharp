using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChainSharp.Logging.Postgres.Services.PostgresContextFactory;

public class PostgresContextFactory(NpgsqlDataSource dataSource) : ILoggingProviderContextFactory
{
    public ILoggingProviderContext Create()
    {
        return new PostgresContext.PostgresContext(
            new DbContextOptionsBuilder<PostgresContext.PostgresContext>()
                .UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention()
                .Options
        );
    }
}
