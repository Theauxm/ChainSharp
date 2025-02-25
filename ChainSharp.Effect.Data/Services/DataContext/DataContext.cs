using System.Data;
using ChainSharp.Effect.Data.Models;
using ChainSharp.Effect.Data.Models.Metadata;
using ChainSharp.Effect.Data.Services.DataContextTransaction;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Services.DataContext;

/// <summary>
/// EFCore DbContext that is meant to act as a wrapper for
/// any implementation. Includes all tables, and common
/// functions necessary to run each workflow.
/// </summary>
/// <param name="options"></param>
public class DataContext<TDbContext>(DbContextOptions<TDbContext> options)
    : DbContext(options),
        IDataContext
    where TDbContext : DbContext
{
    #region Tables

    public DbSet<Metadata> Metadatas { get; set; }
    IQueryable<Metadata> IDataContext.Metadatas => Metadatas;

    #endregion

    public int Changes { get; set; }

    public Task<IDataContextTransaction> BeginTransaction() =>
        BeginTransaction(IsolationLevel.ReadCommitted);

    public async Task<IDataContextTransaction> BeginTransaction(IsolationLevel isolationLevel) =>
        new DataContextTransaction.DataContextTransaction(
            this,
            await Database.BeginTransactionAsync()
        );

    public async Task CommitTransaction() => await Database.CommitTransactionAsync();

    public Task RollbackTransaction() => Database.RollbackTransactionAsync();

    public async Task Track(IModel model)
    {
        Add(model);
    }

    public new async Task<int> SaveChanges() => await base.SaveChangesAsync();

    public void Reset() => ChangeTracker.Clear();
}
