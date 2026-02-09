# ChainSharp

[![Build Status](https://github.com/Theauxm/ChainSharp/workflows/Release%20NuGet%20Package/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)
[![Test Status](https://github.com/Theauxm/ChainSharp/workflows/ChainSharp:%20Run%20CI/CD%20Test%20Suite/badge.svg)](https://github.com/Theauxm/ChainSharp/actions)

## Description

ChainSharp is a .NET library for Railway Oriented Programming, building from functional concepts and attempting to create an encapsulated way of running a piece of code with discrete steps. It aims to simplify complex workflows by providing a clear, linear flow of operations while handling errors and maintaining code readability.

## Available NuGet Packages

ChainSharp is distributed as several NuGet packages, each providing specific functionality:

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

## Architecture Overview

ChainSharp follows a modular architecture where the core library can be extended with optional packages for effects, persistence, and workflow orchestration.

### Dependency Graph

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

### Package Domains

#### Core (`ChainSharp`)

The foundation library providing Railway Oriented Programming patterns. Contains:
- **Workflow<TIn, TOut>**: Base class for defining a sequence of steps that process input to output
- **Step<TIn, TOut>**: Base class for individual units of work within a workflow
- **Chain**: Fluent API for composing steps together (`Activate(input).Chain<Step1>().Chain<Step2>().Resolve()`)

Error handling is built-in using `Either<Exception, T>` monads from LanguageExt—if any step fails, subsequent steps are automatically short-circuited.

#### Effect (`ChainSharp.Effect`)

Extends the core workflow with "effects" (side effects like logging, persistence, and metadata tracking). Contains:
- **EffectWorkflow<TIn, TOut>**: Enhanced workflow with automatic metadata tracking, dependency injection via `[Inject]` attributes, and effect coordination
- **EffectRunner**: Coordinates multiple effect providers and manages their lifecycle
- **IEffectProvider**: Interface for pluggable providers that react to workflow events (track models, save changes)

#### Mediator (`ChainSharp.Effect.Mediator`)

Implements the mediator pattern for workflow discovery and execution. Contains:
- **WorkflowBus**: Discovers and executes workflows based on input type. Allows running workflows from controllers, services, or other workflows
- **WorkflowRegistry**: Scans assemblies at startup to build a mapping of input types → workflow types

> **Note**: Each input type can only map to ONE workflow. Use distinct input types (e.g., `CreateUserRequest`, `UpdateUserRequest`) rather than sharing types across workflows.

#### Data (`ChainSharp.Effect.Data`)

Abstract data persistence layer for storing workflow metadata (execution state, timing, inputs/outputs, errors). Contains:
- **DataContext<T>**: Base EF Core DbContext with `Metadata`, `Log`, and `Manifest` entities
- **IDataContext**: Interface for database operations and transaction management

##### Data.Postgres (`ChainSharp.Effect.Data.Postgres`)

PostgreSQL Data implementation

##### Data.InMemory (`ChainSharp.Effect.Data.InMemory`)

InMemory Data Implementation

#### Provider (`ChainSharp.Effect.Provider.*`)

Effect providers are pluggable components that react to workflow lifecycle events. They implement `IEffectProvider` and are registered via dependency injection.

##### Provider.Json

Debug logging provider that serializes tracked models to JSON and logs state changes. Useful for development and debugging workflow state.

##### Provider.Parameter

Serializes workflow input/output parameters to JSON for database storage. Enables querying workflow history by parameter values and provides an audit trail.

#### StepProvider (`ChainSharp.Effect.StepProvider.*`)

Step-level effect providers that operate on individual steps within a workflow.

##### StepProvider.Logging

Provides structured logging for individual steps, capturing step inputs, outputs, and timing information.

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
