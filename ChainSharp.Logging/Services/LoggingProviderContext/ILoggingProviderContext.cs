using System.Data;
using ChainSharp.Logging.Models;
using ChainSharp.Logging.Models.Metadata;
using ChainSharp.Logging.Services.LoggingProviderContextTransaction;

namespace ChainSharp.Logging.Services.LoggingProviderContext;

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
