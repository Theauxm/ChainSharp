# ChainSharp.Effect.Data Architecture

ChainSharp.Effect.Data provides the persistence layer for ChainSharp.Effect, enabling models created during workflow execution to be saved to a database. This document explains the architecture, key components, and usage patterns of ChainSharp.Effect.Data.

## Introduction

While ChainSharp.Effect creates and tracks models (like Metadata and Log entries) during workflow execution, these models are essentially ephemeral unless they are persisted to a storage medium. ChainSharp.Effect.Data bridges this gap by:

1. Implementing the `IEffectProvider` interface to integrate with the EffectRunner
2. Providing a DbContext-based implementation that can persist models to various database systems
3. Supporting transaction management for database operations
4. Offering both production-ready (PostgreSQL) and testing-friendly (InMemory) implementations

## Core Architecture

### DataContext as an Effect Provider

The central concept in ChainSharp.Effect.Data is the `DataContext<TDbContext>` class, which:

1. Extends Entity Framework Core's `DbContext`
2. Implements the `IEffectProvider` interface from ChainSharp.Effect
3. Provides DbSet properties for Metadata and Log models
4. Implements the Track and SaveChanges methods required by IEffectProvider

This dual nature allows the DataContext to serve as both a traditional EF Core DbContext and an Effect Provider that can be registered with the EffectRunner.

### Model Tracking Flow

The flow of data from model creation to database persistence follows these steps:

1. An `EffectWorkflow` creates a model (e.g., Metadata)
2. The workflow calls `EffectRunner.Track(model)`
3. The EffectRunner forwards the model to all registered effect providers
4. The DataContext (as an effect provider) adds the model to its change tracker
5. When the workflow calls `EffectRunner.SaveChanges()`, the DataContext saves the tracked changes to the database

### Component Diagram

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  EffectWorkflow │────▶│   EffectRunner  │────▶│  DataContext    │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                      │                        │
         │                      │                        │
         ▼                      ▼                        ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    Metadata     │     │ProviderFactory  │     │    Database     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

## Key Components

### IDataContext Interface

The `IDataContext` interface defines the contract for all data contexts in ChainSharp.Effect.Data:

```csharp
public interface IDataContext : IEffectProvider
{
    DbSet<Metadata> Metadatas { get; }
    DbSet<Log> Logs { get; }
    
    int Changes { get; set; }
    Task<IDataContextTransaction> BeginTransaction();
    Task<IDataContextTransaction> BeginTransaction(IsolationLevel isolationLevel);
    Task CommitTransaction();
    Task RollbackTransaction();
    void Reset();
}
```

Key aspects:
- Extends `IEffectProvider` to integrate with the EffectRunner
- Provides access to the Metadata and Log tables
- Supports transaction management
- Includes a Reset method to clear the change tracker

### DataContext<TDbContext> Implementation

The `DataContext<TDbContext>` class provides a generic implementation of `IDataContext` that can wrap any EF Core DbContext:

```csharp
public class DataContext<TDbContext>(DbContextOptions<TDbContext> options)
    : DbContext(options), IDataContext
    where TDbContext : DbContext
{
    public DbSet<Metadata> Metadatas { get; set; }
    public DbSet<Log> Logs { get; set; }
    
    // IEffectProvider implementation
    public async Task SaveChanges(CancellationToken stoppingToken)
    {
        await base.SaveChangesAsync(stoppingToken);
    }

    public async Task Track(IModel model)
    {
        Add(model);
    }
    
    // Transaction management
    public async Task<IDataContextTransaction> BeginTransaction() =>
        BeginTransaction(IsolationLevel.ReadCommitted);

    public async Task<IDataContextTransaction> BeginTransaction(IsolationLevel isolationLevel) =>
        new DataContextTransaction(this, await Database.BeginTransactionAsync());

    public async Task CommitTransaction() => 
        await Database.CommitTransactionAsync();

    public Task RollbackTransaction() => 
        Database.RollbackTransactionAsync();
        
    public void Reset() => ChangeTracker.Clear();
}
```

This implementation:
- Inherits from EF Core's DbContext
- Implements the IDataContext interface
- Provides default implementations for tracking and saving changes
- Wraps EF Core's transaction management

### IDataContextProviderFactory

The `IDataContextProviderFactory` interface extends `IEffectProviderFactory` to create data context instances:

```csharp
public interface IDataContextProviderFactory : IEffectProviderFactory
{
    public Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken);
}
```

This factory pattern allows different database implementations to create their specific context types while maintaining a consistent interface for the EffectRunner.

### DataContextTransaction

The `DataContextTransaction` class provides transaction management for database operations:

```csharp
public class DataContextTransaction : IDataContextTransaction
{
    private readonly IDataContext _dataContext;
    private readonly IDbContextTransaction _transaction;
    
    public DataContextTransaction(IDataContext dataContext, IDbContextTransaction transaction)
    {
        _dataContext = dataContext;
        _transaction = transaction;
    }
    
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
    }
    
    public void Dispose()
    {
        _transaction.Dispose();
    }
}
```

This wrapper allows workflows to manage database transactions explicitly when needed.

## Implementation Examples

### PostgreSQL Implementation

The PostgreSQL implementation provides a production-ready database solution:

#### PostgresContext

```csharp
public class PostgresContext(DbContextOptions<PostgresContext> options)
    : DataContext<PostgresContext>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder
            .HasDefaultSchema("chain_sharp")
            .AddPostgresEnums()
            .ApplyUtcDateTimeConverter();
    }
}
```

The PostgresContext:
- Inherits from DataContext<PostgresContext>
- Sets up a PostgreSQL-specific schema
- Configures PostgreSQL-specific features like enums and UTC date conversion

#### PostgresContextProviderFactory

```csharp
public class PostgresContextProviderFactory(
    IDbContextFactory<PostgresContext> dbContextFactory
) : IDataContextProviderFactory
{
    public int Count { get; private set; } = 0;

    public IEffectProvider Create()
    {
        var context = dbContextFactory.CreateDbContext();
        Count++;
        return context;
    }

    public async Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        Count++;
        return context;
    }
}
```

This factory:
- Uses EF Core's DbContextFactory to create PostgresContext instances
- Tracks the number of contexts created
- Implements both synchronous and asynchronous creation methods

#### Registration

```csharp
public static ChainSharpEffectConfigurationBuilder AddPostgresEffect(
    this ChainSharpEffectConfigurationBuilder configurationBuilder,
    string connectionString
)
{
    DatabaseMigrator.Migrate(connectionString).Wait();

    var dataSource = ModelBuilderExtensions.BuildDataSource(connectionString);

    configurationBuilder.ServiceCollection.AddDbContextFactory<PostgresContext>(
        (_, options) =>
        {
            options
                .UseNpgsql(dataSource)
                .UseLoggerFactory(new NullLoggerFactory())
                .ConfigureWarnings(x => x.Log(CoreEventId.ManyServiceProvidersCreatedWarning));
        }
    );

    configurationBuilder.DataContextLoggingEffectEnabled = true;

    return configurationBuilder.AddEffect<
        IDataContextProviderFactory,
        PostgresContextProviderFactory
    >();
}
```

This extension method:
- Runs database migrations to ensure the schema is up-to-date
- Configures the PostgreSQL connection
- Registers the DbContextFactory and ProviderFactory
- Enables data context logging

### InMemory Implementation

The InMemory implementation provides a lightweight, in-memory database for testing:

#### InMemoryContext

```csharp
public class InMemoryContext(DbContextOptions<InMemoryContext> options)
    : DataContext<InMemoryContext>(options) { }
```

The InMemoryContext is a minimal implementation that inherits all functionality from DataContext.

#### InMemoryContextProviderFactory

```csharp
public class InMemoryContextProviderFactory : IDataContextProviderFactory
{
    public IDataContext Create() =>
        new InMemoryContext(
            new DbContextOptionsBuilder<InMemoryContext>()
                .UseInMemoryDatabase("InMemoryDb")
                .Options
        );

    public async Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return Create();
    }

    IEffectProvider IEffectProviderFactory.Create() => Create();
}
```

This factory:
- Creates InMemoryContext instances with an in-memory database
- Does not require an external DbContextFactory
- Provides a simpler implementation suitable for testing

#### Registration

```csharp
public static ChainSharpEffectConfigurationBuilder AddInMemoryEffect(
    this ChainSharpEffectConfigurationBuilder configurationBuilder
) =>
    configurationBuilder.AddEffect<
        IDataContextProviderFactory,
        InMemoryContextProviderFactory
    >();
```

This extension method simply registers the InMemoryContextProviderFactory with the effect system.

## Integration with EffectRunner

### How EffectRunner Uses IEffectProvider

The EffectRunner manages a collection of effect providers and coordinates their execution:

```csharp
public class EffectRunner : IEffectRunner
{
    private List<IEffectProvider> ActiveEffectProviders { get; init; }

    public EffectRunner(IEnumerable<IEffectProviderFactory> effectProviderFactories)
    {
        ActiveEffectProviders = [];
        ActiveEffectProviders.AddRange(effectProviderFactories.RunAll(factory => factory.Create()));
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await ActiveEffectProviders.RunAllAsync(
            provider => provider.SaveChanges(cancellationToken)
        );
    }

    public async Task Track(IModel model)
    {
        ActiveEffectProviders.RunAll(provider => provider.Track(model));
    }

    public void Dispose() => DeactivateProviders();
}
```

When an EffectWorkflow calls:
- `EffectRunner.Track(model)`: The model is forwarded to all effect providers, including DataContext
- `EffectRunner.SaveChanges()`: All effect providers save their tracked changes, including DataContext

### Lifecycle of a Model

1. **Creation**: An EffectWorkflow creates a model (e.g., Metadata)
   ```csharp
   var metadata = Metadata.Create(
       new CreateMetadata
       {
           Name = WorkflowName,
           Input = input,
           ParentId = ParentId
       }
   );
   ```

2. **Tracking**: The model is tracked by the EffectRunner and forwarded to the DataContext
   ```csharp
   await EffectRunner.Track(metadata);
   ```

3. **Modification**: The workflow updates the model as execution progresses
   ```csharp
   metadata.WorkflowState = WorkflowState.InProgress;
   ```

4. **Persistence**: The changes are saved to the database when SaveChanges is called
   ```csharp
   await EffectRunner.SaveChanges(CancellationToken.None);
   ```

## Usage Examples

### Registering Data Effects

#### PostgreSQL

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .AddEffectDataContextLogging(minimumLogLevel: LogLevel.Information)
);
```

#### InMemory (for testing)

```csharp
services.AddChainSharpEffects(options => 
    options.AddInMemoryEffect()
);
```

### Using Data Contexts in Workflows

Workflows don't need to interact with the data context directly. The EffectRunner handles the integration automatically:

```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        // Metadata is automatically created and tracked
        => Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
        // Metadata is automatically saved when the workflow completes
}
```

### Accessing Data Directly

For cases where direct database access is needed:

```csharp
public class DataAccessStep(IDataContextProviderFactory dataContextFactory)
    : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        using var context = (IDataContext)dataContextFactory.Create();
        
        // Query the database
        var completedWorkflows = await context.Metadatas
            .Where(m => m.WorkflowState == WorkflowState.Completed)
            .ToListAsync();
            
        // Process the results
        foreach (var workflow in completedWorkflows)
        {
            // Do something with the workflow metadata
        }
        
        return Unit.Default;
    }
}
```

### Transaction Management

For operations that require transaction control:

```csharp
public class TransactionalStep(IDataContextProviderFactory dataContextFactory)
    : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        using var context = (IDataContext)dataContextFactory.Create();
        using var transaction = await context.BeginTransaction(IsolationLevel.ReadCommitted);
        
        try
        {
            // Perform multiple database operations
            // Metadata automatically tracked in the .Create() function
            var metadata = Metadata.Create(
                new CreateMetadata { Name = "TransactionalOperation", Input = input }
            );
            
            // More operations...
            
            // Commit the transaction
            await context.SaveChanges(CancellationToken.None);
            await context.CommitTransaction();
            
            return Unit.Default;
        }
        catch (Exception)
        {
            // Rollback on error
            await context.RollbackTransaction();
            throw;
        }
    }
}
```

## Best Practices

### When to Use Different Implementations

- **PostgreSQL**:
  - Production environments
  - When persistent storage is required
  - For complex queries and reporting

- **InMemory**:
  - Unit and integration testing
  - Development and debugging
  - When persistence is not required

### Error Handling

- Use transactions for operations that require atomicity
- Let the Railway pattern handle most errors
- Use specific exception types for database-related errors
- Consider retry policies for transient database failures

### Performance Considerations

- Use asynchronous methods (SaveChangesAsync, etc.) for better scalability
- Call Reset() on the context when you're done with it to clear the change tracker
- Be mindful of the number of entities tracked by the context
- Consider using projection queries (Select()) for read-only operations

### Logging

Enable data context logging to capture database operations:

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .AddEffectDataContextLogging(
            minimumLogLevel: LogLevel.Information,
            blacklist: ["Microsoft.GeneralNamespace.*", "Very.Specific.Namespace"]
        )
);
```

This provides visibility into:
- SQL queries executed
- Transaction boundaries
- Errors and warnings

## Conclusion

ChainSharp.Effect.Data provides a seamless integration between ChainSharp.Effect's model tracking and database persistence. By implementing the IEffectProvider interface, it allows models created during workflow execution to be automatically saved to a database without requiring explicit database code in the workflows themselves.

The architecture supports multiple database implementations, transaction management, and integration with Entity Framework Core, making it a flexible and powerful solution for persisting workflow data.
