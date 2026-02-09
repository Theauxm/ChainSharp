---
layout: default
title: Home
nav_order: 1
---

# ChainSharp

[![Build Status](https://github.com/Theauxm/ChainSharp/workflows/Release%20NuGet%20Package/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)
[![Test Status](https://github.com/Theauxm/ChainSharp/workflows/ChainSharp:%20Run%20CI/CD%20Test%20Suite/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)

ChainSharp is a .NET library for building workflows as a chain of discrete steps.

## The Problem

Multi-step operations tend to accumulate error handling noise:

```csharp
public async Task<User> CreateUser(CreateUserRequest request)
{
    var validated = await _validator.ValidateAsync(request);
    if (!validated.IsValid)
        return Error("Validation failed");

    var user = await _userService.CreateAsync(validated);
    if (user == null)
        return Error("User creation failed");

    var emailSent = await _emailService.SendWelcomeAsync(user);
    if (!emailSent)
        _logger.LogWarning("Welcome email failed for {UserId}", user.Id);

    return user;
}
```

Each step needs its own error check. The actual business logic gets buried.

## With ChainSharp

The same flow, without the noise:

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

If `ValidateUserStep` throws, `CreateUserStep` never runs. The exception propagates automatically. Each step is a separate class with its own dependencies, easy to test in isolation.

For more on how this works, see [Core Concepts](concepts.md).

## Quick Start

### Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect
dotnet add package Theauxm.ChainSharp.Effect.Mediator
```

### Service Registration
```csharp
// Program.cs
services.AddChainSharpEffects(
    o => o.AddEffectWorkflowBus(assemblies: [typeof(AssemblyMarker).Assembly])
);
```

### Running a Workflow
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
