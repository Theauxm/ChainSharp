using System.Data;
using ChainSharp.Logging.Models;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Services.LoggingProviderContextTransaction;

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

    public LoggingProviderContext Raw => (LoggingProviderContext)this;
    public int Changes { get; set; }
    public Task<ILoggingProviderContextTransaction> BeginTransaction();

    public Task<ILoggingProviderContextTransaction> BeginTransaction(IsolationLevel isolationLevel);

    public Task CommitTransaction();

    public Task RollbackTransaction();

    public Task<int> SaveChanges();

    public void Reset();
}
