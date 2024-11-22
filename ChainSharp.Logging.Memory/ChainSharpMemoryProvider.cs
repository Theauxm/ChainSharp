using ChainSharp.Logging.Services.ChainSharpProvider;

namespace ChainSharp.Logging.Memory;

public class ChainSharpMemoryProvider : IChainSharpProvider
{
    public int Changes { get; set; }
    public Task Track()
    {
        throw new NotImplementedException();
    }

    public Task SaveChanges()
    {
        throw new NotImplementedException();
    }

    public Task BeginTransaction(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task Commit()
    {
        throw new NotImplementedException();
    }
}