---
layout: default
title: Home
nav_order: 1
---

# ChainSharp

[![Build Status](https://github.com/Theauxm/ChainSharp/workflows/Release%20NuGet%20Package/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)
[![Test Status](https://github.com/Theauxm/ChainSharp/workflows/ChainSharp:%20Run%20CI/CD%20Test%20Suite/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)

ChainSharp is a .NET library for Railway Oriented Programming, building from functional concepts and attempting to create an encapsulated way of running a piece of code with discrete steps. It aims to simplify complex workflows by providing a clear, linear flow of operations while handling errors and maintaining code readability.

## Core Value Propositions

### Railway Oriented Programming
- **Fail-Fast Design**: Operations either succeed or fail, with automatic error propagation
- **Composable Steps**: Chain operations together with automatic error handling
- **Type Safety**: Strongly typed inputs and outputs with compile-time guarantees

### Effect Pattern
- **Deferred Execution**: Track operations without immediate execution
- **Unit of Work**: Batch operations and execute them atomically
- **Extensible Providers**: Pluggable effects for different concerns (database, logging, etc.)

### Automatic Dependency Injection
- **Attribute-Based**: Use `[Inject]` attribute instead of constructor injection
- **Workflow Discovery**: Automatically find and register workflows based on input types
- **Service Integration**: Seamless integration with .NET dependency injection

## Quick Start Example

### 1. Basic Workflow
```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    [Inject]
    public IEmailService EmailService { get; set; }
    
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
}
```

### 2. Service Registration
```csharp
// Program.cs
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)        // Database persistence
        .SaveWorkflowParameters()                   // Save inputs/outputs
        .AddEffectWorkflowBus(typeof(Program).Assembly) // Auto-discovery
);
```

### 3. Workflow Execution
```csharp
// Controller or service
public class UserController(IWorkflowBus workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<User> CreateUser(CreateUserRequest request)
        => await workflowBus.RunAsync<User>(request);
}
```

## Available NuGet Packages

| Package | Description | Version |
|---------|-------------|---------|
| [Theauxm.ChainSharp](https://www.nuget.org/packages/Theauxm.ChainSharp/) | Core library for Railway Oriented Programming | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp) |
| [Theauxm.ChainSharp.Effect](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect/) | Effects for ChainSharp Workflows | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect) |
| [Theauxm.ChainSharp.Effect.Data](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data/) | Data persistence abstractions for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data) |
| [Theauxm.ChainSharp.Effect.Data.InMemory](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.InMemory/) | In-memory data persistence for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.InMemory) |
| [Theauxm.ChainSharp.Effect.Data.Postgres](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.Postgres/) | PostgreSQL data persistence for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.Postgres) |
| [Theauxm.ChainSharp.Effect.Provider.Json](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Provider.Json/) | JSON serialization for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Provider.Json) |
| [Theauxm.ChainSharp.Effect.Mediator](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Mediator/) | Mediator pattern implementation for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Mediator) |
| [Theauxm.ChainSharp.Effect.Provider.Parameter](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Provider.Parameter/) | Parameter serialization for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Provider.Parameter) |

## When to Choose ChainSharp

### ✅ Ideal For:
- **Complex Business Processes**: Multi-step operations with branching logic
- **Error-Prone Operations**: External API calls, database operations, file processing
- **Audit Requirements**: Need to track what happened, when, and why
- **Microservices**: Coordinating operations across service boundaries
- **Event-Driven Architecture**: Processing events with multiple side effects

### ❌ Consider Alternatives For:
- **Simple CRUD Operations**: Basic database read/write operations
- **High-Frequency, Low-Latency**: Real-time systems where every millisecond counts
- **Stateless Functions**: Pure computational tasks without side effects
- **Legacy Integration**: Systems that can't adopt modern .NET patterns

## Documentation

- [Getting Started](getting-started) - Quick setup guide
- [Core Concepts](concepts) - Essential knowledge for understanding ChainSharp
- [Architecture](architecture) - Detailed system architecture and component relationships
- [Usage Guide](usage-guide) - Practical implementation examples
- [Troubleshooting](troubleshooting) - Common issues and solutions
- [Development](development) - Contributing guidelines and development setup

## License

ChainSharp is licensed under the MIT License.

## Acknowledgements

Without the help and guidance of Mark Keaton this project would not have been possible.
