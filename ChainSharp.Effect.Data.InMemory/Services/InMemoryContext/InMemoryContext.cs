using ChainSharp.Effect.Data.Services.DataContext;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.InMemory.Services.InMemoryContext;

public class InMemoryContext(DbContextOptions<InMemoryContext> options)
    : DataContext<InMemoryContext>(options) { }
