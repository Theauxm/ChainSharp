using System.Data;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Services.LoggingProviderContextTransaction;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging.Services.LoggingProviderContext;

/// <summary>
/// EFCore DbContext that is meant to act as a wrapper for
/// any implementation. Includes all tables, and common
/// functions necessary to run each workflow.
/// </summary>
/// <param name="options"></param>
public class LoggingProviderContext(DbContextOptions<LoggingProviderContext> options)
    : DbContext(options),
        ILoggingProviderContext
{
    #region Tables

    public DbSet<Metadata> Metadatas { get; set; }
    IQueryable<Metadata> ILoggingProviderContext.Metadatas => Metadatas;

    #endregion

    public int Changes { get; set; }

    public Task<ILoggingProviderContextTransaction> BeginTransaction() =>
        BeginTransaction(IsolationLevel.ReadCommitted);

    public async Task<ILoggingProviderContextTransaction> BeginTransaction(
        IsolationLevel isolationLevel
    ) =>
        new LoggingProviderContextTransaction.LoggingProviderContextTransaction(
            this,
            await Database.BeginTransactionAsync()
        );

    public async Task CommitTransaction() => await Database.CommitTransactionAsync();

    public Task RollbackTransaction() => Database.RollbackTransactionAsync();

    public new async Task<int> SaveChanges() => await base.SaveChangesAsync();

    public void Reset() => ChangeTracker.Clear();
}
