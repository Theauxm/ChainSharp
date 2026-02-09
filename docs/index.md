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
- **Workflow Discovery**: Automatically find and register workflows based on input types

## Quick Start Example

### 1. Basic Workflow
```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
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
services.AddChainSharpEffects(
    o => o.AddEffectWorkflowBus(assemblies: [typeof(AssemblyMarker).Assembly])
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

## License

ChainSharp is licensed under the MIT License.

## Acknowledgements

Without the help and guidance of Mark Keaton and Douglas Seely this project would not have been possible.
