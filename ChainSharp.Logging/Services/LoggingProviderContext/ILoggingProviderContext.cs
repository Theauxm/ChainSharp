using System.Data;
using ChainSharp.Logging.Models;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Services.LoggingProviderContextTransaction;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging.Services.LoggingProviderContext;

/// <summary>
/// EFCore DbContext that is meant to act as a wrapper for
/// any implementation. Includes all tables, and common
/// functions necessary to run each workflow.
/// </summary>
public interface ILoggingProviderContext : IDisposable
{
    #region Tables

    IQueryable<Metadata> Metadatas { get; }

    #endregion

    public LoggingProviderContext<TDbContext> Raw<TDbContext>()
        where TDbContext : DbContext => (LoggingProviderContext<TDbContext>)this;
    public int Changes { get; set; }
    public Task<ILoggingProviderContextTransaction> BeginTransaction();

    public Task<ILoggingProviderContextTransaction> BeginTransaction(IsolationLevel isolationLevel);

    public Task CommitTransaction();

    public Task RollbackTransaction();

    public Task Track(IModel model);

    public Task<int> SaveChanges();

    public void Reset();
}
