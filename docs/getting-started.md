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

// Add the dashboard (optional)
builder.Services.AddChainSharpDashboard();

// Add your application services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// Mount the dashboard at /chainsharp (optional)
app.UseChainSharpDashboard("/chainsharp");

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

## Next Steps

- [Dashboard](dashboard) - See your registered workflows in a web UI
- [Core Concepts](concepts) - Learn the fundamental concepts behind ChainSharp
- [Usage Guide](usage-guide) - Explore more advanced patterns and examples
- [Architecture](architecture) - Understand the system architecture in detail
