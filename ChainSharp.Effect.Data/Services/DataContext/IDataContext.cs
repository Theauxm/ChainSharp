using System.Data;
using ChainSharp.Effect.Data.Models.Metadata;
using ChainSharp.Effect.Data.Services.DataContextTransaction;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.Effect;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Services.DataContext;

/// <summary>
/// EFCore DbContext that is meant to act as a wrapper for
/// any implementation. Includes all tables, and common
/// functions necessary to run each workflow.
/// </summary>
public interface IDataContext : IEffect
{
    #region Tables

    IQueryable<Metadata> Metadatas { get; }

    #endregion

    public DataContext<TDbContext> Raw<TDbContext>()
        where TDbContext : DbContext => (DataContext<TDbContext>)this;
    public int Changes { get; set; }
    public Task<IDataContextTransaction> BeginTransaction();

    public Task<IDataContextTransaction> BeginTransaction(IsolationLevel isolationLevel);

    public Task CommitTransaction();

    public Task RollbackTransaction();

    public void Reset();
}
