---
layout: default
title: Getting Started
nav_order: 2
---

# Getting Started

## Installation

### Install NuGet Packages

For a typical setup with database persistence and workflow discovery:

```xml
<PackageReference Include="Theauxm.ChainSharp.Effect" Version="5.*" />
<PackageReference Include="Theauxm.ChainSharp.Effect.Data.Postgres" Version="5.*" />
<PackageReference Include="Theauxm.ChainSharp.Effect.Mediator" Version="5.*" />
```

Or via the .NET CLI:

```bash
dotnet add package Theauxm.ChainSharp.Effect
dotnet add package Theauxm.ChainSharp.Effect.Data.Postgres
dotnet add package Theauxm.ChainSharp.Effect.Mediator
```

## Basic Configuration

### Program.cs Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add ChainSharp services
builder.Services.AddChainSharpEffects(o => o.AddEffectWorkflowBus(typeof(Program).Assembly));

// Add your application services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();
app.Run();
```

## Creating Your First Workflow

### 1. Define Input and Output Models

```csharp
// Input model - unique per workflow
public record CreateUserRequest
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? PhoneNumber { get; init; }
}

// Output model
public record User
{
    public int Id { get; init; }
    public string Email { get; init; }
    public string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

### 2. Implement the Workflow

```csharp
public interface ICreateUserWorkflow : IEffectWorkflow<CreateUserRequest, User> { }

public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>, ICreateUserWorkflow
{
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<ValidateEmailStep>()
            .Chain<CreateUserInDatabaseStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
}
```

### 3. Implement the Steps

```csharp
public class ValidateEmailStep(IUserRepository UserRepository) : Step<CreateUserRequest, Unit>
{
    public override async Task<Unit> Run(CreateUserRequest input)
    {
        // Check if email already exists
        var existingUser = await UserRepository.GetByEmailAsync(input.Email);
        if (existingUser != null)
            throw new ValidationException($"User with email {input.Email} already exists");
        
        // Validate email format
        if (!IsValidEmail(input.Email))
            throw new ValidationException("Invalid email format");
            
        return Unit.Default;
    }
    
    private static bool IsValidEmail(string email)
        => new EmailAddressAttribute().IsValid(email);
}

public class CreateUserInDatabaseStep(IUserRepository UserRepository) : Step<CreateUserRequest, User>
{
    public override async Task<User> Run(CreateUserRequest input)
    {
        var user = new User
        {
            Email = input.Email,
            FullName = $"{input.FirstName} {input.LastName}",
            CreatedAt = DateTime.UtcNow
        };
        
        return await UserRepository.CreateAsync(user);
    }
}

public class SendWelcomeEmailStep(IEmailService EmailService) : Step<User, Unit>
{
    public override async Task<Unit> Run(User input)
    {
        await EmailService.SendWelcomeEmailAsync(input.Email, input.FullName);
        return Unit.Default;
    }
}
```

### 4. Use the Workflow

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController(IWorkflowBus workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
    {
        try
        {
            var user = await workflowBus.RunAsync<User>(request);
            return Created($"/api/users/{user.Id}", user);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "An error occurred while creating the user");
        }
    }
}
```

## Architecture Overview

```
ChainSharp (Core)
    │
    └─── ChainSharp.Effect (Enhanced Workflows)
              │
              ├─── ChainSharp.Effect.Mediator (WorkflowBus)
              │
              ├─── ChainSharp.Effect.Data (Abstract Persistence)
              │         │
              │         ├─── ChainSharp.Effect.Data.Postgres
              │         └─── ChainSharp.Effect.Data.InMemory
              │
              ├─── ChainSharp.Effect.Provider.Json
              ├─── ChainSharp.Effect.Provider.Parameter
              └─── ChainSharp.Effect.StepProvider.Logging
```

## Choosing Effect Providers

ChainSharp has several effect providers. Here's when to use each:

### Database Persistence (Postgres or InMemory)

**Use when:** You need to query workflow history, audit execution, or debug production issues.

```csharp
// Production
services.AddChainSharpEffects(options => 
    options.AddPostgresEffect("Host=localhost;Database=app;Username=postgres;Password=pass")
);

// Testing
services.AddChainSharpEffects(options => 
    options.AddInMemoryEffect()
);
```

This persists a `Metadata` record for each workflow execution containing:
- Workflow name and state (Pending → InProgress → Completed/Failed)
- Start and end timestamps
- Serialized input and output
- Exception details if failed
- Parent workflow ID for nested workflows

### JSON Effect (`AddJsonEffect`)

**Use when:** Debugging during development. Logs workflow state changes to your configured logger.

```csharp
services.AddChainSharpEffects(options => 
    options.AddJsonEffect()
);
```

This doesn't persist anything—it just logs. Useful for seeing what's happening without setting up a database.

### Parameter Effect (`SaveWorkflowParameters`)

**Use when:** You need to store workflow inputs/outputs in the database for later querying or replay.

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()  // Serializes Input/Output to Metadata
);
```

Without this, the `Input` and `Output` columns in `Metadata` are null. With it, they contain JSON-serialized versions of your request and response objects.

### WorkflowBus (`AddEffectWorkflowBus`)

**Use when:** You want to run workflows by input type without manually resolving them.

```csharp
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(typeof(Program).Assembly)
);

// Later:
var user = await workflowBus.RunAsync<User>(new CreateUserRequest { ... });
```

This scans your assemblies for `IEffectWorkflow<TIn, TOut>` implementations and builds a registry. When you call `RunAsync`, it finds the workflow that handles your input type and executes it.

**Note:** Each input type can only map to one workflow. If you have `CreateUserRequest`, only one workflow can accept it.

### Combining Providers

Providers compose. A typical production setup:

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)   // Persist metadata
        .SaveWorkflowParameters()              // Include input/output in metadata
        .AddEffectWorkflowBus(assemblies)      // Enable workflow discovery
);
```

A typical development setup:

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddInMemoryEffect()                   // Fast, no database needed
        .AddJsonEffect()                       // Log state changes
        .AddEffectWorkflowBus(assemblies)
);
```

## Next Steps

- [Core Concepts](concepts) - Learn the fundamental concepts behind ChainSharp
- [Usage Guide](usage-guide) - Explore more advanced patterns and examples
- [Architecture](architecture) - Understand the system architecture in detail
