using ChainSharp.Effect.Data.Services.DataContext;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChainSharp.Effect.Data.Services.DataContextTransaction;

/// <summary>
/// Wraps a Data Context transaction such that the user does not have to mange
/// it themselves.
/// </summary>
/// <param name="db"></param>
/// <param name="tx"></param>
public class DataContextTransaction(IDataContext db, IDbContextTransaction tx)
    : IDataContextTransaction
{
    public async Task Commit() => await db.CommitTransaction();

    public async Task Rollback() => await db.RollbackTransaction();

    public void Dispose() => tx.Dispose();
}
