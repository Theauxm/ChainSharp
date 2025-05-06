# ChainSharp.Effect.Data.Postgres Architecture

ChainSharp.Effect.Data.Postgres provides a PostgreSQL implementation of the ChainSharp.Effect.Data interfaces, enabling models created during workflow execution to be persisted to a PostgreSQL database. This document explains the architecture, key components, and usage patterns specific to the PostgreSQL implementation.

## Introduction

The PostgreSQL implementation offers a production-ready database solution for ChainSharp.Effect workflows. It provides:

1. A PostgreSQL-specific DbContext implementation
2. Automatic database migration and schema management
3. PostgreSQL-specific features like enum support and UTC date conversion
4. Transaction management with PostgreSQL isolation levels
5. Integration with the ChainSharp.Effect.Data architecture

This implementation is designed for production environments where persistent, reliable storage is required.

## Core Components

### PostgresContext

The `PostgresContext` class extends the base `DataContext<TDbContext>` class with PostgreSQL-specific configuration:

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

Key features:
- Uses a dedicated "chain_sharp" schema for all tables
- Configures PostgreSQL-specific enum types
- Applies UTC date/time conversion for consistent timestamp handling

### PostgresContextProviderFactory

The `PostgresContextProviderFactory` creates PostgresContext instances for the EffectRunner:

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

### Database Migration

The `DatabaseMigrator` class handles automatic schema creation and updates:

```csharp
public static class DatabaseMigrator
{
    public static async Task Migrate(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Create schema if it doesn't exist
        await using (var cmd = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS chain_sharp;", connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Apply migrations
        var assembly = typeof(DatabaseMigrator).Assembly;
        var migrationFiles = assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql"))
            .OrderBy(x => x);

        foreach (var migrationFile in migrationFiles)
        {
            await using var stream = assembly.GetManifestResourceStream(migrationFile);
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync();

            await using var cmd = new NpgsqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
```

This migrator:
- Creates the "chain_sharp" schema if it doesn't exist
- Executes SQL migration scripts embedded in the assembly
- Runs migrations in order based on filename

### PostgreSQL-specific Extensions

The implementation includes several PostgreSQL-specific extensions:

#### ModelBuilderExtensions

```csharp
public static class ModelBuilderExtensions
{
    public static ModelBuilder AddPostgresEnums(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<WorkflowState>("chain_sharp", "workflow_state");
        return modelBuilder;
    }

    public static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<WorkflowState>("workflow_state");
        return dataSourceBuilder.Build();
    }
}
```

This extension:
- Maps .NET enums to PostgreSQL enum types
- Configures the NpgsqlDataSource with enum mappings

#### UtcValueConverter

```csharp
public class UtcValueConverter : ValueConverter<DateTime, DateTime>
{
    public UtcValueConverter()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        ) { }
}
```

This converter ensures that all DateTime values are stored and retrieved as UTC.

## Integration with ChainSharp.Effect.Data

The PostgreSQL implementation integrates with ChainSharp.Effect.Data by:

1. Extending the `DataContext<TDbContext>` class
2. Implementing the `IDataContextProviderFactory` interface
3. Providing PostgreSQL-specific extensions to the base functionality

This integration allows the PostgreSQL implementation to be used seamlessly with the EffectRunner and EffectWorkflow components.

## Service Registration

The PostgreSQL implementation is registered with the dependency injection container using the `AddPostgresEffect` extension method:

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

This method:
1. Runs database migrations to ensure the schema is up-to-date
2. Configures the PostgreSQL connection with enum mappings
3. Registers the DbContextFactory for PostgresContext
4. Registers the PostgresContextProviderFactory as an IDataContextProviderFactory
5. Enables data context logging

## Usage in EffectWorkflows

The PostgreSQL implementation is automatically injected into EffectWorkflows through the EffectRunner. Workflows don't need to interact with the PostgreSQL context directly:

```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        // Metadata is automatically created, tracked, and saved to PostgreSQL
        => Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
}
```

When this workflow is executed:
1. The EffectRunner creates a Metadata record
2. The PostgresContext tracks the Metadata
3. When SaveChanges is called, the Metadata is saved to the PostgreSQL database

### Transaction Management

For operations that require explicit transaction control:

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
            // Perform database operations
            // ...
            
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

PostgreSQL supports all standard isolation levels:
- ReadUncommitted
- ReadCommitted (default)
- RepeatableRead
- Serializable

## Advanced Features

### Schema Customization

The PostgreSQL implementation uses a dedicated "chain_sharp" schema by default, but this can be customized:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    modelBuilder
        .HasDefaultSchema("my_custom_schema")
        .AddPostgresEnums()
        .ApplyUtcDateTimeConverter();
}
```

### Migration Management

The embedded SQL migrations handle schema creation and updates automatically. The migration files are:

1. `1_workflow.sql`: Creates the workflow table
2. `2_log.sql`: Creates the log table
3. `3_workflow_input_output.sql`: Adds input/output columns to the workflow table

These migrations are applied in order when the `AddPostgresEffect` method is called.

### PostgreSQL-specific Query Capabilities

The PostgreSQL implementation supports all standard EF Core query capabilities, plus PostgreSQL-specific features:

```csharp
// JSON querying
var workflows = await context.Metadatas
    .Where(m => EF.Functions.JsonContains(m.Input, @"{""userId"": 123}"))
    .ToListAsync();

// Full-text search
var searchResults = await context.Metadatas
    .Where(m => EF.Functions.ToTsVector("english", m.Name).Matches("error"))
    .ToListAsync();
```

## Examples and Best Practices

### Production Configuration

```csharp
// In Program.cs or Startup.cs
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(Configuration.GetConnectionString("PostgreSQL"))
        .AddEffectDataContextLogging(
            minimumLogLevel: LogLevel.Warning,
            blacklist: ["SensitiveTable"]
        )
);
```

### Connection String Best Practices

```
Host=localhost;Database=chain_sharp;Username=postgres;Password=password;Maximum Pool Size=100;Timeout=30;Command Timeout=30;
```

Key settings:
- `Maximum Pool Size`: Adjust based on expected concurrent connections
- `Timeout`: Connection timeout in seconds
- `Command Timeout`: Query timeout in seconds

### Performance Optimization

1. **Use Asynchronous Methods**:
   ```csharp
   await context.SaveChangesAsync(cancellationToken);
   ```

2. **Batch Operations**:
   ```csharp
   // Instead of saving after each operation
   await context.Track(entity1);
   await context.Track(entity2);
   await context.Track(entity3);
   await context.SaveChanges(cancellationToken); // Save once
   ```

3. **Use Projection Queries**:
   ```csharp
   var names = await context.Metadatas
       .Select(m => m.Name)
       .ToListAsync();
   ```

4. **Index Important Columns**:
   ```sql
   CREATE INDEX idx_metadata_name ON chain_sharp.metadata(name);
   ```

### Error Handling

```csharp
try
{
    await context.SaveChanges(cancellationToken);
}
catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
{
    // Handle duplicate key
}
catch (PostgresException ex) when (ex.SqlState == "23503") // Foreign key violation
{
    // Handle referential integrity
}
catch (PostgresException ex)
{
    // Handle other PostgreSQL errors
    logger.LogError(ex, "PostgreSQL error: {Message}, Code: {Code}", ex.Message, ex.SqlState);
}
```

## Conclusion

ChainSharp.Effect.Data.Postgres provides a robust, production-ready implementation of the ChainSharp.Effect.Data interfaces for PostgreSQL databases. It offers automatic schema management, transaction support, and PostgreSQL-specific features while maintaining seamless integration with the ChainSharp.Effect architecture.

By implementing the IDataContextProviderFactory interface, it allows the EffectRunner to automatically persist models to PostgreSQL without requiring explicit database code in the workflows themselves.
