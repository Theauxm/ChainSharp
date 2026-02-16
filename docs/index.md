---
layout: default
title: Home
nav_order: 1
---

# ChainSharp

[![Build Status](https://github.com/Theauxm/ChainSharp/workflows/Release%20NuGet%20Package/badge.svg?branch=main)](https://github.com/Theauxm/ChainSharp/actions)
[![Test Status](https://github.com/Theauxm/ChainSharp/workflows/ChainSharp:%20Run%20CI%2FCD%20Test%20Suite/badge.svg?branch=main)](https://github.com/Theauxm/ChainSharp/actions)

ChainSharp is a .NET library for building workflows as a chain of discrete steps.

## The Problem

Error handling piles up fast:

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

The actual business logic gets buried under null checks and error handling.

## With ChainSharp

The same flow, without the noise:

```csharp
public class ProcessOrderWorkflow : EffectWorkflow<OrderRequest, OrderReceipt>
{
    protected override async Task<Either<Exception, OrderReceipt>> RunInternal(OrderRequest input)
        => Activate(input)
            .Chain<CheckInventoryStep>()
            .Chain<ChargePaymentStep>()
            .Chain<CreateShipmentStep>()
            .Resolve();
}
```

If `CheckInventoryStep` throws, `ChargePaymentStep` never runs. The exception propagates automatically. Each step is a separate class with its own dependencies, easy to test in isolation.

```
Success Track:  Input → [Step 1] → [Step 2] → [Step 3] → Output
                            ↓
Failure Track:          Exception → [Skip] → [Skip] → Exception
```

Remove a step or reorder the chain incorrectly, and the built-in [Analyzer](analyzer.md) tells you at compile time—before you ever run the code.

For more on how this works, see [Core Concepts](concepts.md).

## IDE Extensions

Inlay hint extensions for VSCode and Rider/ReSharper. They show `TIn → TOut` types inline for each `.Chain<TStep>()` call.

- **VSCode** — Install from the [VSCode Marketplace](https://marketplace.visualstudio.com/items?itemName=ChainSharp.chainsharp-hints)
- **Rider / ReSharper** — Search for **ChainSharp Chain Hints** in JetBrains Marketplace

See [IDE Extensions](ide-extensions.md) for details.

## Quick Start

ChainSharp 5.x requires `net10.0`. See [Getting Started](getting-started.md) for installation and your first workflow.

## Available NuGet Packages

| Package | Description | Version |
|---------|-------------|---------|
| [Theauxm.ChainSharp](https://www.nuget.org/packages/Theauxm.ChainSharp/) | Core library for Railway Oriented Programming | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp) |
| [Theauxm.ChainSharp.Effect](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect/) | Effects for ChainSharp Workflows | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect) |
| [Theauxm.ChainSharp.Effect.Dashboard](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Dashboard/) | Web dashboard for inspecting ChainSharp workflows | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Dashboard) |
| [Theauxm.ChainSharp.Effect.Data](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data/) | Data persistence abstractions for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data) |
| [Theauxm.ChainSharp.Effect.Data.InMemory](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.InMemory/) | In-memory data persistence for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.InMemory) |
| [Theauxm.ChainSharp.Effect.Data.Postgres](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.Postgres/) | PostgreSQL data persistence for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.Postgres) |
| [Theauxm.ChainSharp.Effect.Provider.Json](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Provider.Json/) | JSON serialization for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Provider.Json) |
| [Theauxm.ChainSharp.Effect.Orchestration.Mediator](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Orchestration.Mediator/) | Mediator pattern implementation for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Orchestration.Mediator) |
| [Theauxm.ChainSharp.Effect.Provider.Parameter](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Provider.Parameter/) | Parameter serialization for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Provider.Parameter) |
| [Theauxm.ChainSharp.Effect.Orchestration.Scheduler](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Orchestration.Scheduler/) | Manifest-based job scheduling for ChainSharp | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Orchestration.Scheduler) |
| [Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire/) | Hangfire integration for ChainSharp Scheduler | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Orchestration.Scheduler.Hangfire) |

## License

ChainSharp is licensed under the MIT License.

## Acknowledgements

Without the help and guidance of Mark Keaton and Douglas Seely this project would not have been possible.
