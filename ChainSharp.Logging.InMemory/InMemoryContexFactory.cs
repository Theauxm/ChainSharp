using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging.InMemory;

public class InMemoryContextFactory : ILoggingProviderContextFactory
{
    public ILoggingProviderContext Create() =>
        new LoggingProviderContext(
            new DbContextOptionsBuilder<LoggingProviderContext>()
                .UseInMemoryDatabase("TestDb")
                .Options
        );
}
