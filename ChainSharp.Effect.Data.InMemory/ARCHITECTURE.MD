# ChainSharp.Effect.Data.InMemory Architecture

ChainSharp.Effect.Data.InMemory provides a lightweight, in-memory implementation of the ChainSharp.Effect.Data interfaces, enabling models created during workflow execution to be persisted to an in-memory database. This document explains the architecture, key components, and usage patterns specific to the InMemory implementation.

## Introduction

The InMemory implementation offers a lightweight, transient database solution for ChainSharp.Effect workflows. It provides:

1. A simple, in-memory DbContext implementation
2. No database setup or migration requirements
3. Fast, in-process data storage and retrieval
4. Integration with the ChainSharp.Effect.Data architecture

This implementation is designed primarily for:
- Unit and integration testing
- Development and debugging
- Scenarios where persistence beyond the application lifecycle is not required

## Core Components

### InMemoryContext

The `InMemoryContext` class provides a minimal implementation of the base `DataContext<TDbContext>` class:

```csharp
public class InMemoryContext(DbContextOptions<InMemoryContext> options)
    : DataContext<InMemoryContext>(options) { }
```

This implementation:
- Inherits all functionality from the base DataContext
- Uses Entity Framework Core's in-memory database provider
- Requires no additional configuration

The simplicity of this implementation is a key feature - it provides the same interface as other data contexts but with minimal overhead.

### InMemoryContextProviderFactory

The `InMemoryContextProviderFactory` creates InMemoryContext instances for the EffectRunner:

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
- Configures the database name ("InMemoryDb" by default)
- Does not require an external DbContextFactory
- Provides a simpler implementation compared to production database factories

## Integration with ChainSharp.Effect.Data

The InMemory implementation integrates with ChainSharp.Effect.Data by:

1. Extending the `DataContext<TDbContext>` class
2. Implementing the `IDataContextProviderFactory` interface
3. Providing a lightweight alternative to production database implementations

This integration allows the InMemory implementation to be used seamlessly with the EffectRunner and EffectWorkflow components, making it ideal for testing and development scenarios.

## Service Registration

The InMemory implementation is registered with the dependency injection container using the `AddInMemoryEffect` extension method:

```csharp
public static ChainSharpEffectConfigurationBuilder AddInMemoryEffect(
    this ChainSharpEffectConfigurationBuilder configurationBuilder
) =>
    configurationBuilder.AddEffect<
        IDataContextProviderFactory,
        InMemoryContextProviderFactory
    >();
```

This method simply registers the InMemoryContextProviderFactory as an IDataContextProviderFactory with the effect system. Unlike production database implementations, it requires no connection string or additional configuration.

## Usage in EffectWorkflows

The InMemory implementation is automatically injected into EffectWorkflows through the EffectRunner. Workflows don't need to interact with the InMemory context directly:

```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
    {
        // Metadata is automatically created, tracked, and saved to the in-memory database
        return Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
    }
}
```

When this workflow is executed:
1. The EffectRunner creates a Metadata record
2. The InMemoryContext tracks the Metadata
3. When SaveChanges is called, the Metadata is saved to the in-memory database

### Transaction Management

The InMemory implementation supports transactions for API compatibility, but they don't provide true ACID guarantees:

```csharp
public class TransactionalStep(IDataContextProviderFactory dataContextFactory)
    : Step<Unit, Unit>
{
    public override async Task<Unit> Run(Unit input)
    {
        using var context = (IDataContext)dataContextFactory.Create();
        using var transaction = await context.BeginTransaction();
        
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

Note that while the transaction API is supported, the in-memory database provider doesn't provide true transaction isolation. This is one of the key differences between the InMemory implementation and production database implementations.

## Testing Scenarios

### Unit Testing

The InMemory implementation is ideal for unit testing workflows and steps:

```csharp
[Fact]
public async Task TestWorkflow_Success()
{
    // Arrange
    var services = new ServiceCollection()
        .AddChainSharpEffects(options => options.AddInMemoryEffect())
        .AddTransientChainSharpWorkflow<ITestWorkflow, TestWorkflow>()
        .BuildServiceProvider();
        
    var workflowBus = services.GetRequiredService<IWorkflowBus>();
    
    // Act
    var result = await workflowBus.RunAsync<TestWorkflow>(new TestWorkflowInput());
    
    // Assert
    result.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    result.Metadata.FailureException.Should().BeNullOrEmpty();
}
```

### Integration Testing

For integration testing with multiple components:

```csharp
[Fact]
public async Task TestWorkflowWithDependencies()
{
    // Arrange
    var services = new ServiceCollection()
        .AddChainSharpEffects(options => options.AddInMemoryEffect())
        .AddTransientChainSharpWorkflow<ITestWorkflow, TestWorkflow>()
        .AddScoped<IUserRepository, UserRepository>() // Real implementation
        .AddScoped<IEmailService, FakeEmailService>() // Test double
        .BuildServiceProvider();
        
    var workflowBus = services.GetRequiredService<IWorkflowBus>();
    
    // Act
    var result = await workflowBus.RunAsync<TestWorkflow>(new TestWorkflowInput());
    
    // Assert
    result.Metadata.WorkflowState.Should().Be(WorkflowState.Completed);
    
    // Verify data was saved
    var dataContextFactory = services.GetRequiredService<IDataContextProviderFactory>();
    using var context = (IDataContext)dataContextFactory.Create();
    var savedMetadata = await context.Metadatas.FirstOrDefaultAsync(m => m.Id == result.Metadata.Id);
    savedMetadata.Should().NotBeNull();
}
```

### Test Data Setup

Setting up test data is straightforward:

```csharp
public async Task SetupTestData(IDataContextProviderFactory dataContextFactory)
{
    using var context = (IDataContext)dataContextFactory.Create();
    
    // Create test metadata
    var metadata = Metadata.Create(
        new CreateMetadata { Name = "TestMetadata", Input = new TestInput() }
    );
    metadata.WorkflowState = WorkflowState.Completed;
    
    await context.Track(metadata);
    await context.SaveChanges(CancellationToken.None);
}
```

### Test Data Isolation

Each test can use a different database name to ensure isolation:

```csharp
public class IsolatedTestContextProviderFactory : IDataContextProviderFactory
{
    private readonly string _databaseName;
    
    public IsolatedTestContextProviderFactory(string databaseName)
    {
        _databaseName = databaseName;
    }
    
    public IDataContext Create() =>
        new InMemoryContext(
            new DbContextOptionsBuilder<InMemoryContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options
        );
    
    public async Task<IDataContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return Create();
    }
    
    IEffectProvider IEffectProviderFactory.Create() => Create();
}
```

Usage:
```csharp
services.AddSingleton<IDataContextProviderFactory>(
    new IsolatedTestContextProviderFactory($"Test_{Guid.NewGuid()}")
);
```

## Comparison with Production Implementations

### Advantages

1. **Simplicity**: No database setup, connection strings, or migrations
2. **Speed**: In-memory operations are faster than database I/O
3. **Isolation**: Each test can use a separate database instance
4. **No External Dependencies**: No need for a database server

### Limitations

1. **Transient Storage**: Data is lost when the application stops
2. **Limited Query Capabilities**: No support for advanced database features
3. **No True Transactions**: Transaction API is supported but not fully implemented
4. **No Concurrency Control**: Limited support for concurrent access patterns

## Examples and Best Practices

### Basic Configuration

```csharp
// In Program.cs or Startup.cs (development environment)
if (env.IsDevelopment())
{
    services.AddChainSharpEffects(options => options.AddInMemoryEffect());
}
else
{
    services.AddChainSharpEffects(options => 
        options.AddPostgresEffect(Configuration.GetConnectionString("PostgreSQL"))
    );
}
```

### Testing Configuration

```csharp
// In a test setup class
public class TestFixture
{
    public ServiceProvider ServiceProvider { get; }
    
    public TestFixture()
    {
        ServiceProvider = new ServiceCollection()
            .AddLogging()
            .AddChainSharpEffects(options => options.AddInMemoryEffect())
            .AddTransientChainSharpWorkflow<ITestWorkflow, TestWorkflow>()
            // Register other services
            .BuildServiceProvider();
    }
}
```

### Database Name Customization

```csharp
// Custom factory registration
services.AddSingleton<IDataContextProviderFactory>(provider => 
    new InMemoryContextProviderFactory("CustomDatabaseName")
);

// Or custom extension method
public static ChainSharpEffectConfigurationBuilder AddInMemoryEffect(
    this ChainSharpEffectConfigurationBuilder configurationBuilder,
    string databaseName
) =>
    configurationBuilder.AddEffect<
        IDataContextProviderFactory,
        CustomInMemoryContextProviderFactory
    >(new CustomInMemoryContextProviderFactory(databaseName));
```

### Performance Considerations

While the InMemory implementation is generally faster than production databases, there are still some performance considerations:

1. **Memory Usage**: Large datasets can consume significant memory
2. **Query Performance**: Complex queries may not be optimized as well as in production databases
3. **Entity Tracking**: EF Core still tracks entities, which can impact performance with large datasets

### Transitioning to Production

When transitioning from development/testing to production:

1. **Keep Service Interfaces Consistent**: Use the same interfaces regardless of the implementation
2. **Use Configuration to Switch Implementations**: Don't hardcode the implementation choice
3. **Test with Both Implementations**: Ensure your code works with both InMemory and production databases
4. **Be Aware of Feature Differences**: Some database-specific features may not work with InMemory

## Conclusion

ChainSharp.Effect.Data.InMemory provides a lightweight, easy-to-use implementation of the ChainSharp.Effect.Data interfaces for in-memory databases. It offers simplicity and speed for testing and development scenarios while maintaining the same API as production database implementations.

By implementing the IDataContextProviderFactory interface, it allows the EffectRunner to automatically persist models to an in-memory database without requiring explicit database code in the workflows themselves, making it an ideal choice for unit testing, integration testing, and development environments.
