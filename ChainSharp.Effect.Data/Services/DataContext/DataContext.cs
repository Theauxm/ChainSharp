using System.Data;
using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Models.Metadata;
using ChainSharp.Effect.Data.Services.DataContextTransaction;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Metadata;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyEntityOnModelCreating();
    }

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

    public async Task SaveChanges()
    {
        await base.SaveChangesAsync();
    }

    public async Task Track(IModel model)
    {
        Add(model);
    }

    public void Reset() => ChangeTracker.Clear();
}
