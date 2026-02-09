---
layout: default
title: Development
nav_order: 7
---

# Development Guide

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
├── ChainSharp.Effect.Provider.Json/      # JSON logging effect
├── ChainSharp.Effect.Mediator/           # Workflow discovery and routing
├── ChainSharp.Effect.Provider.Parameter/ # Parameter serialization
├── ChainSharp.Effect.StepProvider.Logging/ # Step-level logging
└── tests/
    ├── ChainSharp.Tests/                 # Core unit tests
    ├── ChainSharp.Tests.ArrayLogger/     # Array-based logging for tests
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

# Build all projects
dotnet build

# Build specific project
dotnet build ChainSharp/ChainSharp.csproj
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ChainSharp.Tests.Unit/ChainSharp.Tests.Unit.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"
```

### Database Migrations

```bash
# Add a new migration
dotnet ef migrations add MigrationName \
  --project ChainSharp.Effect.Data.Postgres \
  --startup-project ChainSharp.Effect.Data.Postgres

# Apply migrations
dotnet ef database update \
  --project ChainSharp.Effect.Data.Postgres \
  --startup-project ChainSharp.Effect.Data.Postgres

# Revert last migration
dotnet ef migrations remove \
  --project ChainSharp.Effect.Data.Postgres
```

## Code Style Guidelines

### C# Conventions

```csharp
// Use file-scoped namespaces
namespace ChainSharp.Effect;

// Use primary constructors where appropriate
public class MyService(ILogger<MyService> logger) : IMyService
{
    public void DoSomething() => logger.LogInformation("Doing something");
}

// Use expression-bodied members for simple methods
public string GetName() => _name;

// Use records for DTOs
public record CreateUserRequest(string Email, string Name);

// Use init-only properties
public class User
{
    public required string Email { get; init; }
    public string? PhoneNumber { get; init; }
}
```

### Async/Await Patterns

```csharp
// Always use async suffix for async methods
public async Task<User> CreateUserAsync(CreateUserRequest request)

// Use ConfigureAwait(false) in library code
var result = await _service.GetDataAsync().ConfigureAwait(false);

// Use ValueTask for frequently synchronous paths
public ValueTask<User?> GetCachedUserAsync(int id)
{
    if (_cache.TryGetValue(id, out var user))
        return new ValueTask<User?>(user);
    
    return new ValueTask<User?>(LoadUserAsync(id));
}
```

### Error Handling

```csharp
// Use Either for expected failures
public Either<ValidationError, User> ValidateUser(UserInput input)

// Use exceptions for unexpected failures
if (connection == null)
    throw new InvalidOperationException("Database connection not initialized");

// Create specific exception types
public class WorkflowException : Exception
{
    public string WorkflowName { get; }
    public WorkflowState State { get; }
}
```

## Testing Guidelines

### Unit Test Structure

```csharp
public class ValidateEmailStepTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly ValidateEmailStep _sut;
    
    public ValidateEmailStepTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _sut = new ValidateEmailStep { UserRepository = _mockRepo.Object };
    }
    
    [Fact]
    public async Task Run_WithValidEmail_ReturnsInput()
    {
        // Arrange
        var input = new CreateUserRequest { Email = "test@example.com" };
        _mockRepo.Setup(r => r.GetByEmailAsync(input.Email))
            .ReturnsAsync((User?)null);
        
        // Act
        var result = await _sut.Run(input);
        
        // Assert
        result.IsRight.Should().BeTrue();
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("@missing-local.com")]
    public async Task Run_WithInvalidEmail_ReturnsError(string email)
    {
        // Arrange
        var input = new CreateUserRequest { Email = email };
        
        // Act
        var result = await _sut.Run(input);
        
        // Assert
        result.IsLeft.Should().BeTrue();
    }
}
```

### Integration Test Setup

```csharp
public class WorkflowIntegrationTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider;
    private IWorkflowBus _workflowBus;
    
    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddChainSharpEffects(options => 
            options
                .AddInMemoryEffect()
                .AddEffectWorkflowBus(typeof(TestWorkflow).Assembly)
        );
        
        _serviceProvider = services.BuildServiceProvider();
        _workflowBus = _serviceProvider.GetRequiredService<IWorkflowBus>();
    }
    
    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }
    
    [Fact]
    public async Task Workflow_CompletesCycle_Successfully()
    {
        // Test implementation
    }
}
```

## Contributing

### Pull Request Process

1. **Fork the repository** and create your branch from `main`
2. **Write tests** for any new functionality
3. **Update documentation** if you're changing APIs
4. **Run the full test suite** before submitting
5. **Create a pull request** with a clear description

### Commit Message Format

```
type(scope): subject

body

footer
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Example:
```
feat(mediator): add workflow caching for improved performance

Implement LRU cache for workflow instances to reduce DI resolution overhead.
Cache size is configurable via WorkflowBusOptions.

Closes #123
```

## Release Process

### Version Numbering

ChainSharp follows [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking API changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

### Publishing NuGet Packages

```bash
# Build release version
dotnet build -c Release

# Pack NuGet packages
dotnet pack -c Release -o ./artifacts

# Push to NuGet (requires API key)
dotnet nuget push ./artifacts/*.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```
