using System.Data;
using ChainSharp.Effect.Data.Extensions;
using ChainSharp.Effect.Data.Models.Metadata;
using ChainSharp.Effect.Data.Services.DataContextTransaction;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Log;
using ChainSharp.Effect.Models.Metadata;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Services.DataContext;

/// <summary>
/// Provides a generic implementation of IDataContext that can be used with any Entity Framework Core DbContext.
/// This class serves as a base for specific database implementations like PostgresContext and InMemoryContext.
/// </summary>
/// <typeparam name="TDbContext">The specific DbContext type</typeparam>
/// <param name="options">The options to be used by the DbContext</param>
/// <remarks>
/// The DataContext class is a key component in the ChainSharp.Effect.Data system.
/// It implements the IDataContext interface and extends Entity Framework Core's DbContext,
/// providing a bridge between the ChainSharp.Effect tracking system and database persistence.
/// 
/// This class:
/// 1. Implements IDataContext to integrate with the ChainSharp.Effect system
/// 2. Extends DbContext to leverage Entity Framework Core's ORM capabilities
/// 3. Provides DbSet properties for Metadata and Log entities
/// 4. Implements transaction management
/// 5. Implements the Track and SaveChanges methods required by IEffectProvider
/// 
/// The generic type parameter TDbContext allows this class to be used with different
/// DbContext implementations, making it adaptable to various database systems.
/// </remarks>
public class DataContext<TDbContext>(DbContextOptions<TDbContext> options)
    : DbContext(options),
        IDataContext
    where TDbContext : DbContext
{
    #region Tables

    /// <summary>
    /// Gets or sets the DbSet for workflow metadata records.
    /// </summary>
    /// <remarks>
    /// This property provides access to the Metadata table, which stores information about
    /// workflow executions, including inputs, outputs, state, and timing information.
    /// 
    /// The Metadatas DbSet is the primary storage mechanism for workflow tracking data
    /// and is used by the EffectRunner to persist workflow execution details.
    /// </remarks>
    public DbSet<Metadata> Metadatas { get; set; }

    /// <summary>
    /// Gets or sets the DbSet for workflow log entries.
    /// </summary>
    /// <remarks>
    /// This property provides access to the Log table, which stores detailed log entries
    /// generated during workflow execution.
    /// 
    /// The Logs DbSet allows for fine-grained tracking of workflow execution steps
    /// and is particularly useful for debugging and auditing.
    /// </remarks>
    public DbSet<Log> Logs { get; set; }

    #endregion

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context</param>
    /// <remarks>
    /// This method is called when the model for a derived context has been initialized, but
    /// before the model has been locked down and used to initialize the context.
    /// 
    /// It applies entity configurations using the ApplyEntityOnModelCreating extension method,
    /// which scans for entity configuration methods and applies them to the model builder.
    /// 
    /// Derived contexts can override this method to further configure the model,
    /// such as adding database-specific configurations.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyEntityOnModelCreating();
    }

    /// <summary>
    /// Gets or sets the number of changes tracked by the context.
    /// </summary>
    /// <remarks>
    /// This property tracks the number of entities that have been modified but not yet
    /// persisted to the database. It can be used to determine if there are pending changes
    /// that need to be saved.
    /// </remarks>
    public int Changes { get; set; }

    /// <summary>
    /// Begins a new database transaction with the default isolation level (ReadCommitted).
    /// </summary>
    /// <returns>A transaction object that can be used to commit or rollback changes</returns>
    /// <remarks>
    /// This method starts a new database transaction with the ReadCommitted isolation level.
    /// The transaction must be explicitly committed or rolled back using the CommitTransaction
    /// or RollbackTransaction methods.
    /// 
    /// Transactions ensure that multiple database operations are treated as a single atomic unit,
    /// either all succeeding or all failing together.
    /// </remarks>
    public Task<IDataContextTransaction> BeginTransaction() =>
        BeginTransaction(IsolationLevel.ReadCommitted);

    /// <summary>
    /// Begins a new database transaction with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The transaction isolation level to use</param>
    /// <returns>A transaction object that can be used to commit or rollback changes</returns>
    /// <remarks>
    /// This method starts a new database transaction with the specified isolation level.
    /// The isolation level determines how the transaction interacts with other concurrent transactions.
    /// 
    /// The method creates a new DataContextTransaction that wraps the underlying EF Core transaction,
    /// providing a consistent interface for transaction management across different database implementations.
    /// 
    /// The transaction must be explicitly committed or rolled back using the
    /// CommitTransaction or RollbackTransaction methods.
    /// </remarks>
    public async Task<IDataContextTransaction> BeginTransaction(IsolationLevel isolationLevel) =>
        new DataContextTransaction.DataContextTransaction(
            this,
            await Database.BeginTransactionAsync()
        );

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method commits the current transaction, making all changes permanent.
    /// It should be called after all operations within the transaction have completed successfully.
    /// 
    /// If no transaction is active, this method may throw an exception or have no effect,
    /// depending on the database provider.
    /// </remarks>
    public async Task CommitTransaction() => await Database.CommitTransactionAsync();

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method rolls back the current transaction, discarding all changes made within it.
    /// It should be called when an error occurs within the transaction and the changes
    /// should not be persisted.
    /// 
    /// If no transaction is active, this method may throw an exception or have no effect,
    /// depending on the database provider.
    /// </remarks>
    public Task RollbackTransaction() => Database.RollbackTransactionAsync();

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="stoppingToken">A token to monitor for cancellation requests</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method persists all tracked changes to the database. It is called by the
    /// EffectRunner when SaveChanges is called on the runner.
    /// 
    /// This implementation delegates to the base SaveChangesAsync method, which handles
    /// the actual persistence of changes to the database.
    /// </remarks>
    public async Task SaveChanges(CancellationToken stoppingToken)
    {
        await base.SaveChangesAsync(stoppingToken);
    }

    /// <summary>
    /// Begins tracking the specified model in the Added state.
    /// </summary>
    /// <param name="model">The model to track</param>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method adds the specified model to the context's change tracker in the Added state,
    /// indicating that it should be inserted into the database when SaveChanges is called.
    /// 
    /// This implementation is called by the EffectRunner when Track is called on the runner,
    /// allowing workflow metadata to be automatically persisted to the database.
    /// </remarks>
    public async Task Track(IModel model)
    {
        Add(model);
    }

    /// <summary>
    /// Resets the context, clearing all tracked entities.
    /// </summary>
    /// <remarks>
    /// This method clears the change tracker, removing all entities that are being tracked
    /// by the context. This is useful when the context has been used for a long time
    /// and may be tracking many entities, which can impact performance.
    /// 
    /// After calling Reset, any entities that were previously tracked will need to be
    /// re-attached to the context if they need to be persisted.
    /// </remarks>
    public void Reset() => ChangeTracker.Clear();
}
