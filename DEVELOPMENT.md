# ChainSharp Development Guide

## Development Environment Setup and Contributing Guidelines

This guide covers setting up your development environment, development practices, and contributing to ChainSharp projects.

## Development Environment Setup

### Prerequisites

Before you begin, ensure you have:
- [Docker Desktop](https://www.docker.com/products/docker-desktop) - For devcontainer and database services
- [Visual Studio Code](https://code.visualstudio.com/) - Primary development environment
- [Remote - Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) - For devcontainer support

### Using the Development Container

ChainSharp provides a fully configured development container that includes all necessary tools and dependencies.

#### Quick Start
1. **Clone the repository**:
   ```bash
   git clone https://github.com/yourorg/ChainSharp.git
   cd ChainSharp
   ```

2. **Open in VS Code**:
   ```bash
   code .
   ```

3. **Reopen in Container**:
   - VS Code will detect the devcontainer configuration
   - Click "Reopen in Container" when prompted
   - Or use Command Palette (F1) → "Remote-Containers: Reopen in Container"

4. **Wait for Setup**:
   - Container builds automatically (first time takes 5-10 minutes)
   - All dependencies are installed automatically
   - PostgreSQL database is started and configured

#### What's Included

The devcontainer provides:
- **.NET SDK 8.0** - Latest stable version
- **PostgreSQL 15** - Database with sample schema
- **Entity Framework Tools** - For migrations and database management
- **VS Code Extensions**:
  - C# Dev Kit
  - REST Client for API testing
  - GitLens for Git history
  - Docker extension
  - PostgreSQL management tools

#### Database Configuration

The devcontainer automatically configures PostgreSQL with:
- **Host**: `database` (container name)
- **Database**: `chain_sharp`
- **Username**: `chain_sharp`
- **Password**: `chain_sharp123`
- **Connection String**: `Host=database;Database=chain_sharp;Username=chain_sharp;Password=chain_sharp123`

### Manual Setup (Alternative)

If you prefer not to use the devcontainer:

#### Install Dependencies
```bash
# Install .NET SDK 8.0
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest

# Install Entity Framework Tools
dotnet tool install --global dotnet-ef

# Install PostgreSQL (example for macOS)
brew install postgresql
brew services start postgresql
```

#### Database Setup
```bash
# Create database
createdb chain_sharp

# Create user
psql -d chain_sharp -c "CREATE USER chain_sharp WITH ENCRYPTED PASSWORD 'chain_sharp123';"
psql -d chain_sharp -c "GRANT ALL PRIVILEGES ON DATABASE chain_sharp TO chain_sharp;"
```

#### Connection String Configuration
```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=chain_sharp;Username=chain_sharp;Password=chain_sharp123"
  }
}
```

## Project Structure

### Solution Organization

```
ChainSharp/
├── ChainSharp/                           # Core workflow engine
├── ChainSharp.Effect/                    # Effect system and enhanced workflows
├── ChainSharp.Effect.Data/               # Database persistence interfaces
├── ChainSharp.Effect.Data.InMemory/      # In-memory database implementation
├── ChainSharp.Effect.Data.Postgres/      # PostgreSQL implementation
├── ChainSharp.Effect.Json/               # JSON logging effect
├── ChainSharp.Effect.Mediator/           # Workflow discovery and routing
├── ChainSharp.Effect.Parameter/          # Parameter serialization
├── ChainSharp.ArrayLogger/               # Array-based logging effect
├── ChainSharp.Blazor/                    # Blazor integration (future)
└── Tests/
    ├── ChainSharp.Tests/                 # Core unit tests
    ├── ChainSharp.Tests.Integration/     # Integration tests
    ├── ChainSharp.Tests.Unit/            # Additional unit tests
    ├── ChainSharp.Tests.Effect.*/        # Effect-specific tests
    └── ChainSharp.Tests.MemoryLeak.Integration/ # Memory leak tests
```

### Naming Conventions

#### Projects
- **Core projects**: `ChainSharp.*`
- **Effect providers**: `ChainSharp.Effect.*`
- **Test projects**: `ChainSharp.Tests.*`

#### Classes
- **Workflows**: `*Workflow` (e.g., `CreateUserWorkflow`)
- **Steps**: `*Step` (e.g., `ValidateEmailStep`)
- **Effect Providers**: `*EffectProvider` or `*Effect` (e.g., `JsonEffectProvider`)
- **Interfaces**: `I*` prefix (e.g., `IWorkflowBus`)

#### Files
- **Interfaces**: Match class name (e.g., `IWorkflowBus.cs`)
- **Implementations**: Match class name (e.g., `WorkflowBus.cs`)
- **Extensions**: `*Extensions.cs` (e.g., `ServiceExtensions.cs`)

## Development Workflow

### Building the Solution

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build ChainSharp.Effect/ChainSharp.Effect.csproj

# Build for release
dotnet build --configuration Release
```

### Running Tests

#### All Tests
```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

#### Specific Test Projects
```bash
# Unit tests only
dotnet test ChainSharp.Tests.Unit/

# Integration tests
dotnet test ChainSharp.Tests.Integration/

# Memory leak tests
dotnet test ChainSharp.Tests.MemoryLeak.Integration/

# Effect-specific tests
dotnet test ChainSharp.Tests.Effect.Data.Postgres.Integration/
```

#### Test Categories

**Unit Tests**: Fast, isolated tests with no external dependencies
```bash
# Example unit test
[Fact]
public async Task EffectRunner_Track_AddsModelToProviders()
{
    // Arrange
    var mockProvider = new Mock<IEffectProvider>();
    var effectRunner = new EffectRunner(new[] { mockProvider.Object });
    var model = new TestModel();
    
    // Act
    await effectRunner.Track(model);
    
    // Assert
    mockProvider.Verify(p => p.Track(model), Times.Once);
}
```

**Integration Tests**: Test component interactions with real dependencies
```bash
# Example integration test
[Fact]
public async Task WorkflowBus_RunAsync_PersistsMetadata()
{
    // Uses real database, real workflows, real effects
    var result = await _workflowBus.RunAsync<User>(new CreateUserRequest { /* ... */ });
    
    // Verify in database
    var metadata = await _dataContext.Metadatas.FirstAsync();
    metadata.WorkflowState.Should().Be(WorkflowState.Completed);
}
```

**Memory Leak Tests**: Verify no memory leaks in workflow execution
```bash
# Example memory leak test
[Fact]
public async Task Workflow_DoesNotLeak_Memory()
{
    var initialMemory = MemoryProfiler.GetCurrentMemoryUsage();
    
    for (int i = 0; i < 100; i++)
    {
        await _workflowBus.RunAsync<TestOutput>(new TestInput());
    }
    
    MemoryProfiler.ForceGarbageCollection();
    var finalMemory = MemoryProfiler.GetCurrentMemoryUsage();
    
    (finalMemory - initialMemory).Should().BeLessThan(10_000_000);
}
```

### Database Migrations

#### Creating Migrations
```bash
# Navigate to data project
cd ChainSharp.Effect.Data.Postgres/

# Create new migration
dotnet ef migrations add AddNewFeature --startup-project ../ChainSharp.Tests.Effect.Data.Postgres.Integration/

# Update database
dotnet ef database update --startup-project ../ChainSharp.Tests.Effect.Data.Postgres.Integration/
```

#### Manual SQL Migrations
For PostgreSQL-specific features, create manual SQL migrations:

```sql
-- ChainSharp.Effect.Data.Postgres/Migrations/4_add_workflow_tags.sql
CREATE TABLE IF NOT EXISTS chain_sharp.workflow_tags (
    id SERIAL PRIMARY KEY,
    metadata_id INTEGER NOT NULL REFERENCES chain_sharp.metadata(id),
    tag_name VARCHAR(50) NOT NULL,
    tag_value TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_workflow_tags_metadata_id ON chain_sharp.workflow_tags(metadata_id);
CREATE INDEX idx_workflow_tags_name ON chain_sharp.workflow_tags(tag_name);
```

## Testing Strategies

### Test Structure

#### Arrange-Act-Assert Pattern
```csharp
[Fact]
public async Task CreateUserWorkflow_ValidInput_ReturnsUser()
{
    // Arrange
    var services = CreateTestServices();
    var workflow = services.GetRequiredService<ICreateUserWorkflow>();
    var input = new CreateUserRequest { Email = "test@example.com", /* ... */ };
    
    // Act
    var result = await workflow.Run(input);
    
    // Assert
    result.IsRight.Should().BeTrue();
    var user = result.ValueUnsafe();
    user.Email.Should().Be("test@example.com");
}
```

#### Test Fixtures for Shared Setup
```csharp
public class WorkflowTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public IWorkflowBus WorkflowBus { get; }
    public IDataContextProviderFactory DataContextFactory { get; }
    
    public WorkflowTestFixture()
    {
        ServiceProvider = new ServiceCollection()
            .AddChainSharpEffects(options => 
                options
                    .AddInMemoryEffect()
                    .SaveWorkflowParameters()
                    .AddEffectWorkflowBus(typeof(TestWorkflow).Assembly)
            )
            .AddTestServices()
            .BuildServiceProvider();
            
        WorkflowBus = ServiceProvider.GetRequiredService<IWorkflowBus>();
        DataContextFactory = ServiceProvider.GetRequiredService<IDataContextProviderFactory>();
    }
    
    public void Dispose() => ServiceProvider.Dispose();
}

[CollectionDefinition("Workflow Tests")]
public class WorkflowTestCollection : ICollectionFixture<WorkflowTestFixture> { }

[Collection("Workflow Tests")]
public class CreateUserWorkflowTests
{
    private readonly WorkflowTestFixture _fixture;
    
    public CreateUserWorkflowTests(WorkflowTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task TestMethod()
    {
        // Use _fixture.WorkflowBus, etc.
    }
}
```

### Mocking and Test Doubles

#### Creating Test Services
```csharp
public static class TestServiceExtensions
{
    public static IServiceCollection AddTestServices(this IServiceCollection services)
    {
        // Real implementations for core ChainSharp services
        services.AddChainSharpEffects(options => options.AddInMemoryEffect());
        
        // Test doubles for external dependencies
        services.AddSingleton<IEmailService, TestEmailService>();
        services.AddSingleton<IPaymentService, TestPaymentService>();
        services.AddSingleton<IExternalApiClient, TestApiClient>();
        
        return services;
    }
}

public class TestEmailService : IEmailService
{
    public List<EmailSent> SentEmails { get; } = new();
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        SentEmails.Add(new EmailSent { To = to, Subject = subject, Body = body });
        await Task.Delay(10); // Simulate async operation
    }
}
```

#### Verifying Side Effects
```csharp
[Fact]
public async Task CreateUserWorkflow_Success_SendsWelcomeEmail()
{
    // Arrange
    var emailService = _fixture.ServiceProvider.GetRequiredService<IEmailService>() as TestEmailService;
    var input = new CreateUserRequest { Email = "test@example.com", FirstName = "John" };
    
    // Act
    await _fixture.WorkflowBus.RunAsync<User>(input);
    
    // Assert
    emailService.SentEmails.Should().ContainSingle(email => 
        email.To == "test@example.com" && 
        email.Subject.Contains("Welcome"));
}
```

## Performance Testing

### Memory Leak Detection

The project includes specialized memory leak tests:

```csharp
public class MemoryLeakTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task WorkflowExecution_DoesNotLeak_Memory(int iterations)
    {
        // Setup
        var services = CreateTestServices();
        var workflowBus = services.GetRequiredService<IWorkflowBus>();
        
        // Baseline
        MemoryProfiler.ForceGarbageCollection();
        var initialMemory = MemoryProfiler.GetCurrentMemoryUsage();
        
        // Execute workflows
        for (int i = 0; i < iterations; i++)
        {
            await workflowBus.RunAsync<TestOutput>(new TestInput { Id = i });
        }
        
        // Measure final memory
        MemoryProfiler.ForceGarbageCollection();
        var finalMemory = MemoryProfiler.GetCurrentMemoryUsage();
        
        // Assert no significant memory growth
        var memoryGrowth = finalMemory - initialMemory;
        var maxAllowedGrowth = iterations * 1_000_000; // 1MB per iteration
        
        memoryGrowth.Should().BeLessThan(maxAllowedGrowth, 
            $"Memory grew by {memoryGrowth:N0} bytes for {iterations} iterations");
    }
}
```

### Performance Benchmarking

For performance-critical code, use BenchmarkDotNet:

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class WorkflowPerformanceBenchmark
{
    private IServiceProvider _serviceProvider;
    private IWorkflowBus _workflowBus;
    
    [GlobalSetup]
    public void Setup()
    {
        _serviceProvider = new ServiceCollection()
            .AddChainSharpEffects(options => options.AddInMemoryEffect())
            .AddTestServices()
            .BuildServiceProvider();
            
        _workflowBus = _serviceProvider.GetRequiredService<IWorkflowBus>();
    }
    
    [Benchmark]
    public async Task SimpleWorkflow()
    {
        await _workflowBus.RunAsync<SimpleOutput>(new SimpleInput());
    }
    
    [Benchmark]
    public async Task ComplexWorkflow()
    {
        await _workflowBus.RunAsync<ComplexOutput>(new ComplexInput());
    }
}
```

## Debugging and Troubleshooting

### Enabling Detailed Logging

```csharp
// In test or development setup
services.AddLogging(builder => 
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Trace)
        .AddFilter("ChainSharp", LogLevel.Trace)
        .AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information)
);
```

### Using Debug Configuration

```csharp
// Add to test configuration for debugging
services.AddChainSharpEffects(options => 
    options
        .AddInMemoryEffect()
        .AddJsonEffect() // Logs all model changes
        .SaveWorkflowParameters()
        .AddEffectWorkflowBus(assemblies)
);
```

### Debugging Workflow Execution

```csharp
[Fact]
public async Task DebugWorkflowExecution()
{
    var result = await _workflowBus.RunAsync<Output>(input);
    
    // Inspect metadata
    using var dataContext = (IDataContext)_dataContextFactory.Create();
    var metadata = await dataContext.Metadatas
        .Include(m => m.Logs)
        .FirstAsync(m => m.Name == nameof(TestWorkflow));
    
    // Output debug information
    Console.WriteLine($"Workflow State: {metadata.WorkflowState}");
    Console.WriteLine($"Start Time: {metadata.StartTime}");
    Console.WriteLine($"End Time: {metadata.EndTime}");
    Console.WriteLine($"Input: {metadata.Input}");
    Console.WriteLine($"Output: {metadata.Output}");
    
    if (metadata.FailureException != null)
    {
        Console.WriteLine($"Error: {metadata.FailureException}");
        Console.WriteLine($"Stack Trace: {metadata.StackTrace}");
    }
    
    foreach (var log in metadata.Logs.OrderBy(l => l.Timestamp))
    {
        Console.WriteLine($"[{log.Timestamp}] {log.LogLevel}: {log.Message}");
    }
}
```

## Contributing Guidelines

### Code Style

- **Follow C# conventions**: PascalCase for public members, camelCase for private
- **Use meaningful names**: `CreateUserWorkflow` not `CUW`
- **Keep methods small**: Aim for single responsibility
- **Document public APIs**: XML comments for public interfaces

### Pull Request Process

1. **Create Feature Branch**:
   ```bash
   git checkout -b feature/add-new-effect-provider
   ```

2. **Implement Changes**:
   - Write code following existing patterns
   - Add comprehensive tests
   - Update documentation if needed

3. **Test Thoroughly**:
   ```bash
   # Run all tests
   dotnet test
   
   # Run memory leak tests
   dotnet test ChainSharp.Tests.MemoryLeak.Integration/
   
   # Check test coverage
   dotnet test --collect:"XPlat Code Coverage"
   ```

4. **Commit with Clear Messages**:
   ```bash
   git commit -m "feat: add Redis effect provider for distributed caching

   - Implement RedisEffectProvider for caching workflow results
   - Add configuration options for Redis connection
   - Include comprehensive unit and integration tests
   - Update documentation with Redis setup instructions"
   ```

5. **Submit Pull Request**:
   - Provide clear description of changes
   - Reference any related issues
   - Include testing instructions

### Common Development Tasks

#### Adding a New Effect Provider

1. **Create the Provider**:
   ```csharp
   public class CustomEffectProvider : IEffectProvider
   {
       public async Task Track(IModel model) { /* implementation */ }
       public async Task SaveChanges(CancellationToken cancellationToken) { /* implementation */ }
       public void Dispose() { /* cleanup */ }
   }
   ```

2. **Create the Factory**:
   ```csharp
   public class CustomEffectProviderFactory : IEffectProviderFactory
   {
       public IEffectProvider Create() => new CustomEffectProvider();
   }
   ```

3. **Add Extension Method**:
   ```csharp
   public static ChainSharpEffectConfigurationBuilder AddCustomEffect(
       this ChainSharpEffectConfigurationBuilder builder)
   {
       return builder.AddEffect<IEffectProviderFactory, CustomEffectProviderFactory>();
   }
   ```

4. **Add Tests**:
   - Unit tests for the provider
   - Integration tests with workflows
   - Memory leak tests

#### Adding a New Workflow

1. **Define Input/Output Models**:
   ```csharp
   public record ProcessOrderRequest { /* properties */ }
   public record ProcessOrderResult { /* properties */ }
   ```

2. **Create Interface**:
   ```csharp
   public interface IProcessOrderWorkflow : IEffectWorkflow<ProcessOrderRequest, ProcessOrderResult> { }
   ```

3. **Implement Workflow**:
   ```csharp
   public class ProcessOrderWorkflow : EffectWorkflow<ProcessOrderRequest, ProcessOrderResult>, IProcessOrderWorkflow
   {
       protected override async Task<Either<Exception, ProcessOrderResult>> RunInternal(ProcessOrderRequest input)
           => Activate(input)
               .Chain<ValidateOrderStep>()
               .Chain<ProcessPaymentStep>()
               .Chain<FulfillOrderStep>()
               .Resolve();
   }
   ```

4. **Add Tests**:
   - Unit tests for workflow logic
   - Integration tests with database
   - Error handling tests

This development guide provides the foundation for contributing to ChainSharp. Follow these patterns and practices to maintain code quality and consistency across the project.
