using ChainSharp.Logging.Services.LoggingProviderContext;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging.InMemory.Services.InMemoryContext;

public class InMemoryContext(DbContextOptions<InMemoryContext> options)
    : LoggingProviderContext<InMemoryContext>(options) { }
