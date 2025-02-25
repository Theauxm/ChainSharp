using ChainSharp.Logging.Services.LoggingProviderContext;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChainSharp.Logging.Services.LoggingProviderContextTransaction;

/// <summary>
/// Wraps a Data Context transaction such that the user does not have to mange
/// it themselves.
/// </summary>
/// <param name="db"></param>
/// <param name="tx"></param>
public class LoggingProviderContextTransaction(ILoggingProviderContext db, IDbContextTransaction tx)
    : ILoggingProviderContextTransaction
{
    public async Task Commit() => await db.CommitTransaction();

    public async Task Rollback() => await db.RollbackTransaction();

    public void Dispose() => tx.Dispose();
}
