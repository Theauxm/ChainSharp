---
layout: default
title: Getting Started
nav_order: 2
---

# Getting Started

This guide will walk you through setting up ChainSharp in your project and creating your first workflow.

## Installation

### Install NuGet Packages

For a typical setup with database persistence and workflow discovery:

```xml
<PackageReference Include="Theauxm.ChainSharp.Effect" Version="1.0.0" />
<PackageReference Include="Theauxm.ChainSharp.Effect.Data.Postgres" Version="1.0.0" />
<PackageReference Include="Theauxm.ChainSharp.Effect.Mediator" Version="1.0.0" />
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
builder.Services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(builder.Configuration.GetConnectionString("PostgreSQL"))
        .SaveWorkflowParameters()
        .AddEffectWorkflowBus(typeof(Program).Assembly)
);

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
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    [Inject]
    public IEmailService EmailService { get; set; }
    
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
public class ValidateEmailStep : Step<CreateUserRequest, CreateUserRequest>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    public override async Task<Either<Exception, CreateUserRequest>> Run(CreateUserRequest input)
    {
        // Check if email already exists
        var existingUser = await UserRepository.GetByEmailAsync(input.Email);
        if (existingUser != null)
            return new ValidationException($"User with email {input.Email} already exists");
        
        // Validate email format
        if (!IsValidEmail(input.Email))
            return new ValidationException("Invalid email format");
            
        return input; // Pass through unchanged
    }
    
    private static bool IsValidEmail(string email)
        => new EmailAddressAttribute().IsValid(email);
}

public class CreateUserInDatabaseStep : Step<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
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

public class SendWelcomeEmailStep : Step<User, User>
{
    [Inject]
    public IEmailService EmailService { get; set; }
    
    public override async Task<User> Run(User input)
    {
        await EmailService.SendWelcomeEmailAsync(input.Email, input.FullName);
        return input; // Pass through unchanged
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

## Common Configuration Patterns

### Development with JSON Logging

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddInMemoryEffect()
        .AddJsonEffect()
);
```

### Production with Mediator

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters()
        .AddEffectWorkflowBus(assemblies)
);
```

## Next Steps

- [Core Concepts](concepts) - Learn the fundamental concepts behind ChainSharp
- [Usage Guide](usage-guide) - Explore more advanced patterns and examples
- [Architecture](architecture) - Understand the system architecture in detail
