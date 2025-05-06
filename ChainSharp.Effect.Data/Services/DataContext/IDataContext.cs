using System.Data;
using ChainSharp.Effect.Data.Models.Metadata;
using ChainSharp.Effect.Data.Services.DataContextTransaction;
using ChainSharp.Effect.Models;
using ChainSharp.Effect.Models.Log;
using ChainSharp.Effect.Models.Metadata;
using ChainSharp.Effect.Services.EffectProvider;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Services.DataContext;

/// <summary>
/// Defines the contract for a database context that integrates with the ChainSharp.Effect system.
/// This interface extends IEffectProvider to enable database persistence of workflow metadata.
/// </summary>
/// <remarks>
/// The IDataContext interface is a central abstraction in the ChainSharp.Effect.Data system.
/// It serves as a bridge between the ChainSharp.Effect tracking system and database persistence,
/// allowing workflow metadata to be stored in various database systems.
/// 
/// This interface:
/// 1. Extends IEffectProvider to integrate with the EffectRunner
/// 2. Provides access to the Metadata and Log tables
/// 3. Supports transaction management
/// 4. Allows access to the underlying DbContext implementation
/// 
/// Different database implementations (PostgreSQL, InMemory, etc.) implement this interface
/// to provide consistent behavior while leveraging specific database features.
/// </remarks>
public interface IDataContext : IEffectProvider
{
    #region Tables

    /// <summary>
    /// Gets the DbSet for workflow metadata records.
    /// </summary>
    /// <remarks>
    /// This property provides access to the Metadata table, which stores information about
    /// workflow executions, including inputs, outputs, state, and timing information.
    /// 
    /// The Metadatas DbSet is the primary storage mechanism for workflow tracking data
    /// and is used by the EffectRunner to persist workflow execution details.
    /// </remarks>
    DbSet<Metadata> Metadatas { get; }

    /// <summary>
    /// Gets the DbSet for workflow log entries.
    /// </summary>
    /// <remarks>
    /// This property provides access to the Log table, which stores detailed log entries
    /// generated during workflow execution.
    /// 
    /// The Logs DbSet allows for fine-grained tracking of workflow execution steps
    /// and is particularly useful for debugging and auditing.
    /// </remarks>
    DbSet<Log> Logs { get; }

    #endregion

    /// <summary>
    /// Gets the raw DbContext implementation for advanced operations.
    /// </summary>
    /// <typeparam name="TDbContext">The specific DbContext type</typeparam>
    /// <returns>The underlying DataContext implementation</returns>
    /// <remarks>
    /// This method provides access to the concrete DataContext implementation,
    /// allowing for advanced operations that may not be exposed through the IDataContext interface.
    /// 
    /// Use this method with caution, as it bypasses the abstraction provided by IDataContext
    /// and may lead to implementation-specific code.
    /// </remarks>
    DataContext<TDbContext> Raw<TDbContext>()
        where TDbContext : DbContext => (DataContext<TDbContext>)this;
    
    /// <summary>
    /// Gets or sets the number of changes tracked by the context.
    /// </summary>
    /// <remarks>
    /// This property tracks the number of entities that have been modified but not yet
    /// persisted to the database. It can be used to determine if there are pending changes
    /// that need to be saved.
    /// </remarks>
    int Changes { get; set; }
    
    /// <summary>
    /// Begins a new database transaction with the default isolation level.
    /// </summary>
    /// <returns>A transaction object that can be used to commit or rollback changes</returns>
    /// <remarks>
    /// This method starts a new database transaction with the default isolation level
    /// (typically ReadCommitted). The transaction must be explicitly committed or rolled back
    /// using the CommitTransaction or RollbackTransaction methods.
    /// 
    /// Transactions ensure that multiple database operations are treated as a single atomic unit,
    /// either all succeeding or all failing together.
    /// </remarks>
    Task<IDataContextTransaction> BeginTransaction();

    /// <summary>
    /// Begins a new database transaction with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The transaction isolation level to use</param>
    /// <returns>A transaction object that can be used to commit or rollback changes</returns>
    /// <remarks>
    /// This method starts a new database transaction with the specified isolation level.
    /// The isolation level determines how the transaction interacts with other concurrent transactions.
    /// 
    /// Common isolation levels include:
    /// - ReadUncommitted: Allows dirty reads (reading uncommitted changes from other transactions)
    /// - ReadCommitted: Prevents dirty reads but allows non-repeatable reads
    /// - RepeatableRead: Prevents dirty reads and non-repeatable reads
    /// - Serializable: Provides the highest isolation, preventing all concurrency issues
    /// 
    /// The transaction must be explicitly committed or rolled back using the
    /// CommitTransaction or RollbackTransaction methods.
    /// </remarks>
    Task<IDataContextTransaction> BeginTransaction(IsolationLevel isolationLevel);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    /// <remarks>
    /// This method commits the current transaction, making all changes permanent.
    /// It should be called after all operations within the transaction have completed successfully.
    /// 
    /// If no transaction is active, this method may throw an exception or have no effect,
    /// depending on the implementation.
    /// </remarks>
    Task CommitTransaction();

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
    /// depending on the implementation.
    /// </remarks>
    Task RollbackTransaction();

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
    void Reset();
}
