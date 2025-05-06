# ChainSharp

[![Build Status](https://github.com/Theauxm/ChainSharp/workflows/Release%20NuGet%20Package/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)
[![Test Status](https://github.com/Theauxm/ChainSharp/workflows/ChainSharp:%20Run%20CI/CD%20Test%20Suite/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)

## Description

ChainSharp is a .NET library for Railway Oriented Programming, building from functional concepts and attempting to create an encapsulated way of running a piece of code with discrete steps. It aims to simplify complex workflows by providing a clear, linear flow of operations while handling errors and maintaining code readability.

## Features

- **Railway Oriented Programming**: Implements the Railway Oriented Programming paradigm for clear, linear, and maintainable workflows.
- **Functional Concepts**: Leverages functional programming concepts to enhance code clarity and robustness.
- **Encapsulated Steps**: Encapsulates each step of a process, making the code easy to read, write, and maintain.
- **Error Handling**: Built-in mechanisms for handling errors at each step without disrupting the overall flow.
- **Open Source**: Fully open source under the MIT license.

## Installation

You can install ChainSharp via NuGet. Run the following command in your package manager console:

```sh
dotnet add package Theauxm.ChainSharp
```

Or, you can add it directly to your `.csproj` file:

```csharp
<PackageReference Include="Theauxm.ChainSharp" Version="..." />
```

## Available NuGet Packages

ChainSharp is distributed as several NuGet packages, each providing specific functionality:

| Package | Description | Version |
|---------|-------------|---------|
| [Theauxm.ChainSharp](https://www.nuget.org/packages/Theauxm.ChainSharp/) | Core library for Railway Oriented Programming | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp) |
| [Theauxm.ChainSharp.Effect](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect/) | Effects for ChainSharp Workflows | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect) |
| [Theauxm.ChainSharp.Effect.Data](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data/) | Data persistence abstractions for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data) |
| [Theauxm.ChainSharp.Effect.Data.InMemory](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.InMemory/) | In-memory data persistence for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.InMemory) |
| [Theauxm.ChainSharp.Effect.Data.Postgres](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Data.Postgres/) | PostgreSQL data persistence for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Data.Postgres) |
| [Theauxm.ChainSharp.Effect.Json](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Json/) | JSON serialization for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Json) |
| [Theauxm.ChainSharp.Effect.Mediator](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Mediator/) | Mediator pattern implementation for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Mediator) |
| [Theauxm.ChainSharp.Effect.Parameter](https://www.nuget.org/packages/Theauxm.ChainSharp.Effect.Parameter/) | Parameter serialization for ChainSharp Effects | ![NuGet Version](https://img.shields.io/nuget/v/Theauxm.ChainSharp.Effect.Parameter) |

## Usage Examples

### Basic Workflow

A workflow in ChainSharp represents a sequence of steps that process data in a linear fashion. Here's a basic example of a workflow:

```csharp
using ChainSharp.Exceptions;
using ChainSharp.Workflow;
using LanguageExt;

// Define a workflow that takes Ingredients as input and produces a List<GlassBottle> as output
public class Cider
    : Workflow<Ingredients, List<GlassBottle>>,
        ICider
{
    // Implement the RunInternal method to define the workflow steps
    protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(
        Ingredients input
    ) => Activate(input) 
            .Chain<Prepare>() // Chain steps together, passing the output of one step as input to the next
            .Chain<Ferment>()
            .Chain<Brew>()
            .Chain<Bottle>()
            .Resolve(); // Resolve the final result
}
```

### Step Anatomy

Steps are the building blocks of workflows. Each step performs a specific operation and can be chained together to form a complete workflow. Here's an example of a step:

```csharp
using ChainSharp.Exceptions;
using ChainSharp.Step;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;

// Define a step that takes Ingredients as input and produces a BrewingJug as output
public class Prepare : Step<Ingredients, BrewingJug>, IPrepare
{
    // Implement the Run method to define the step's operation
    public override async Task<BrewingJug> Run(Ingredients input)
    {
        const int gallonWater = 1;

        // Perform the step's operation
        var gallonAppleJuice = Boil(gallonWater, input.Apples, input.BrownSugar);

        // Handle errors
        if (gallonAppleJuice < 0)
            throw new Exception("Couldn't make a Gallon of Apple Juice!")

        // Return the result
        return new BrewingJug() { Gallons = gallonAppleJuice, Ingredients = input };
    }

    // Helper method for the step
    private async int Boil(
        int gallonWater,
        int numApples,
        int ozBrownSugar
    ) => gallonWater + (numApples / 8) + (ozBrownSugar / 128);
}
```

### EffectWorkflow

EffectWorkflows extend the basic workflow concept by adding support for effects, which allow for side effects like logging, data persistence, and more. Here's an example of an EffectWorkflow:

```csharp
using ChainSharp.Effect.Services.EffectWorkflow;
using LanguageExt;

// Define an EffectWorkflow that takes TestEffectWorkflowInput as input and produces TestEffectWorkflow as output
public class ExampleEffectWorkflow
    : EffectWorkflow<WorkflowInput, WorkflowOutput>
        IExampleEffectWorkflow
{
    // Implement the RunInternal method to define the workflow steps
    protected override async Task<Either<Exception, WorkflowOutput>> RunInternal(
        WorkflowInput input
    ) => Activate(input)
        .Chain<StepOne>
        .Chain<StepTwo>
        .Chain<StepThree>
        .Resolve();
}

// Define the interface for the workflow
public interface IExampleEffectWorkflow
    : IEffectWorkflow<WorkflowInput, WorkflowOutput> { }
```

### WorkflowBus

The WorkflowBus provides a way to run workflows from anywhere in your application, such as from controllers, services, or even other workflows. Here are some examples of using the WorkflowBus:

#### Using WorkflowBus in a Step

```csharp
using ChainSharp.Step;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using Microsoft.Extensions.Logging;
using LanguageExt;

// Define a step that runs another workflow
internal class StepToRunNestedWorkflow(IWorkflowBus workflowBus) : Step<Unit, IInternalWorkflow>
{
    public override async Task<IInternalWorkflow> Run(Unit input)
    {
        // Run a workflow using the WorkflowBus
        var testWorkflow = await workflowBus.RunAsync<IInternalWorkflow>(
            input
        );

        return testWorkflow;
    }
}
```

#### Using WorkflowBus in a Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using ChainSharp.Effect.Mediator.Services.WorkflowBus;
using ChainSharp.Exceptions;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IWorkflowBus _workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        try
        {
            // Run a workflow using the WorkflowBus
            var order = await _workflowBus.RunAsync<Order>(request);
            return Ok(order);
        }
        catch (WorkflowException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        try
        {
            // Run a workflow using the WorkflowBus
            var order = await _workflowBus.RunAsync<Order>(new GetOrderRequest { Id = id });
            return Ok(order);
        }
        catch (WorkflowException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (WorkflowException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
```

#### Registering WorkflowBus with Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using ChainSharp.Effect.Extensions;

// Register ChainSharp services with dependency injection
new ServiceCollection()
    .AddChainSharpEffects(
        options =>
            options
                .AddEffectWorkflowBus(
                    assemblies:
                    [
                        typeof(AssemblyMarker).Assembly
                    ]
                )
    );
```

## Documentation

For detailed documentation and API references, please visit the [official documentation.](https://github.com/Theauxm/ChainSharp/wiki)

## Contributing

Contributions are welcome! Please read our Contributing Guide to learn about our development process, how to propose bugfixes and improvements, and how to build and test your changes.

## License

ChainSharp is licensed under the MIT License.

## Contact

If you have any questions or suggestions, feel free to open an issue.

## Acknowledgements

Without the help and guidance of Mark Keaton this project would not have been possible.
