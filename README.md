# ChainSharp

[![Build Status](https://github.com/Theauxm/ChainSharp/workflows/Release%20NuGet%20Package/badge.svg?branch=main)](https://github.com/Theauxm/ChainSharp/actions)
[![Test Status](https://github.com/Theauxm/ChainSharp/workflows/ChainSharp:%20Run%20CI%2FCD%20Test%20Suite/badge.svg?branch=main)](https://github.com/Theauxm/ChainSharp/actions)

A .NET workflow engine built on Railway Oriented Programming. Each workflow is a chain of discrete steps with automatic error propagation—if a step fails, the rest are skipped.

```
Success Track:  Input → [Step 1] → [Step 2] → [Step 3] → Output
                            ↓
Failure Track:          Exception → [Skip] → [Skip] → Exception
```

## The Problem

Error handling buries your business logic:

```csharp
public async Task<OrderReceipt> ProcessOrder(OrderRequest request)
{
    var inventory = await _inventory.CheckAsync(request.Items);
    if (!inventory.Available)
        return Error("Items out of stock");

    var payment = await _payments.ChargeAsync(request.PaymentMethod, request.Total);
    if (!payment.Success)
        return Error("Payment failed");

    var shipment = await _shipping.CreateAsync(request.Address, request.Items);
    if (shipment == null)
        return Error("Shipping setup failed");

    return new OrderReceipt(payment, shipment);
}
```

## With ChainSharp

```csharp
public class ProcessOrderWorkflow : ServiceTrain<OrderRequest, OrderReceipt>, IProcessOrderWorkflow
{
    protected override async Task<Either<Exception, OrderReceipt>> RunInternal(OrderRequest input)
        => Activate(input)
            .Chain<CheckInventoryStep>()
            .Chain<ChargePaymentStep>()
            .Chain<CreateShipmentStep>()
            .Resolve();
}
```

Each step is a single-responsibility class with constructor-injected dependencies. If `CheckInventoryStep` throws, `ChargePaymentStep` and `CreateShipmentStep` never run.

## Features

- **Railway Oriented Programming** — Two-track execution with `Either<Exception, T>` from LanguageExt
- **Effect System** — Atomic workflows where effects (database writes, logs) only persist on success
- **Metadata Tracking** — Every execution recorded with timing, inputs/outputs, and failure details
- **Roslyn Analyzer** — Compile-time validation that step input/output types chain correctly
- **IDE Extensions** — Inlay hints showing `TIn → TOut` types for each chain call ([VSCode](https://marketplace.visualstudio.com/items?itemName=ChainSharp.chainsharp-hints), Rider/ReSharper)
- **Workflow Discovery** — Register workflows once, dispatch by input type via `IWorkflowBus`
- **Job Scheduling** — Manifest-based scheduling with retries and dead-lettering via Hangfire
- **Web Dashboard** — Blazor UI for inspecting registered workflows and execution history

## Installation

```bash
dotnet add package Theauxm.ChainSharp.Effect
```

Add packages as you need them — persistence, workflow discovery, scheduling, etc. See [Packages](#packages) below.

## Quick Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChainSharpEffects(o => o.AddEffectWorkflowBus(typeof(Program).Assembly));

var app = builder.Build();
app.Run();
```

## Packages

| Package | Description | Version |
|---------|-------------|---------|
| [Theauxm.ChainSharp](https://www.nuget.org/packages/Theauxm.ChainSharp/) | Core workflow engine | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp) |
| [Theauxm.ChainSharp.Effect](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect/) | Effects, metadata tracking, DI | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect) |
| [Theauxm.ChainSharp.Effect.Data](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data/) | Persistence abstractions | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data) |
| [Theauxm.ChainSharp.Effect.Data.Postgres](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.Postgres/) | PostgreSQL persistence | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.Postgres) |
| [Theauxm.ChainSharp.Effect.Data.InMemory](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.InMemory/) | In-memory persistence (testing) | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.InMemory) |
| [Theauxm.ChainSharp.Effect.Orchestration.Mediator](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Orchestration.Mediator/) | Workflow discovery and routing | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Orchestration.Mediator) |
| [Theauxm.ChainSharp.Effect.Orchestration.Scheduler](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Orchestration.Scheduler/) | Manifest-based job scheduling | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Orchestration.Scheduler) |
| [Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire/) | Hangfire integration | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire) |
| [Theauxm.ChainSharp.Effect.Dashboard](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Dashboard/) | Blazor web dashboard | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Dashboard) |
| [Theauxm.ChainSharp.Effect.Provider.Json](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Provider.Json/) | JSON effect logging | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Provider.Json) |
| [Theauxm.ChainSharp.Effect.Provider.Parameter](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Provider.Parameter/) | Parameter serialization | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Provider.Parameter) |
| [Theauxm.ChainSharp.Effect.StepProvider.Logging](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.StepProvider.Logging/) | Step-level structured logging | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.StepProvider.Logging) |

## Documentation

**[Full Documentation](https://theauxm.github.io/ChainSharp/)** — Getting started, core concepts, usage patterns, architecture, and more.

## Contributing

Contributions are welcome. This project uses [Conventional Commits](https://www.conventionalcommits.org/) for versioning — see [Semantic Release](https://theauxm.github.io/ChainSharp/semantic-release) for details.

## License

MIT

## Acknowledgements

Without the help and guidance of Mark Keaton and Douglas Seely this project would not have been possible.
