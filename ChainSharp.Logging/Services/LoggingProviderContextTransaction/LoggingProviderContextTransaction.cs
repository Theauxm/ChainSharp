using ChainSharp.Logging.Services.LoggingProviderContext;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChainSharp.Logging.Services.LoggingProviderContextTransaction;

public class LoggingProviderContextTransaction(ILoggingProviderContext db, IDbContextTransaction tx)
    : ILoggingProviderContextTransaction
{
    public async Task Commit() => await db.CommitTransaction();

    public async Task Rollback() => await db.RollbackTransaction();

    public void Dispose() => tx.Dispose();
}
