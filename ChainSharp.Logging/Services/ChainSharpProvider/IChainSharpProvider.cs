using Microsoft.EntityFrameworkCore.Storage;

namespace ChainSharp.Logging.Services.ChainSharpProvider;

public interface IChainSharpProvider
{
    public int Changes { get; set; }
    public Task Track();
    public Task SaveChanges();
    public Task<IDbContextTransaction> BeginTransaction(CancellationToken cancellationToken);
    
    public Task Commit();

}