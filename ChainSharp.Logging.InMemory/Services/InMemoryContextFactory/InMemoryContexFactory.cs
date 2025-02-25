using ChainSharp.Logging.Services.LoggingProviderContext;
using ChainSharp.Logging.Services.LoggingProviderContextFactory;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging.InMemory.Services.InMemoryContextFactory;

/// <summary>
/// InMemory Provider Context Factory implementation.
/// </summary>
public class InMemoryContextFactory : ILoggingProviderContextFactory
{
    public ILoggingProviderContext Create() =>
        new InMemoryContext.InMemoryContext(
            new DbContextOptionsBuilder<InMemoryContext.InMemoryContext>()
                .UseInMemoryDatabase("InMemoryDb")
                .Options
        );
}
