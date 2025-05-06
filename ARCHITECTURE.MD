# ChainSharp Architecture

ChainSharp is a .NET library for Railway Oriented Programming, providing a structured way to build workflows with discrete steps. This document explains the core architecture and concepts of ChainSharp.

## Example: Building a Workflow

Here's a simple example of a workflow that processes ingredients to make cider:

```csharp
public class Cider : Workflow<Ingredients, List<GlassBottle>>, ICider
{
    protected override async Task<Either<Exception, List<GlassBottle>>> RunInternal(Ingredients input)
        => Activate(input)
            .Chain<Prepare>()
            .Chain<Ferment>()
            .Chain<Brew>()
            .Chain<Bottle>()
            .Resolve();
}
```

This workflow takes `Ingredients` as input, processes them through a series of steps (prepare, ferment, brew, bottle), and produces a list of `GlassBottle` objects as output.

## Memory Management in Workflows

### How Memory Works

ChainSharp workflows maintain state through a `Memory` dictionary that stores objects by their type. This dictionary is the core mechanism for passing data between steps in a workflow.

```csharp
protected internal Dictionary<Type, object> Memory { get; private set; } = null!;
```

Key aspects of memory management:

1. **Type-Based Storage**: Objects are stored with their Type as the key
2. **Interface Registration**: When an object is added to memory, all its interfaces are also registered
3. **Tuple Handling**: Tuple elements are extracted and stored individually
4. **Automatic Output Storage**: Step outputs are automatically stored in memory

### Memory Lifecycle

1. **Initialization**: Memory is initialized when `Activate()` is called or during the first `Run()` call
2. **Population**: Objects are added to memory through:
   - The `Activate()` method (for initial inputs)
   - Step outputs (from `Chain()` calls)
   - The `AddServices()` method (for dependencies)
3. **Retrieval**: Objects are retrieved from memory when:
   - A step requires an input type
   - The `Resolve()` method is called to get the final result

### Type Resolution Order

When a step requires an input of type `T`, ChainSharp looks for it in this order:

1. Directly in the Memory dictionary
2. From the IServiceProvider (if available)
3. From ILoggerFactory (for ILogger<T> types)

## Service Provider Integration

### Injecting a Service Provider

A service provider can be injected into a workflow in two ways:

1. **Via Run Method**:
   ```csharp
   var result = await workflow.Run(input, serviceProvider);
   ```

2. **Via Memory**:
   ```csharp
   workflow.Activate(input).AddServices(serviceProvider);
   ```

### Service Resolution

When a step requires a dependency, ChainSharp will:

1. Check if the type exists in Memory
2. If not, try to resolve it from the IServiceProvider
3. For ILogger<T>, try to create it from an ILoggerFactory in Memory

### Adding Services Directly

Services can be added directly to a workflow's memory:

```csharp
workflow.AddServices(service1, service2);
```

This registers the services by their interface types, making them available to steps.

## Step-Workflow Interaction

### Step Definition

Steps are defined by implementing the `IStep<TIn, TOut>` interface:

```csharp
public interface IStep<TIn, TOut>
{
    Task<TOut> Run(TIn input);
    Task<Either<Exception, TOut>> RailwayStep(Either<Exception, TIn> previousOutput);
}
```

The base `Step<TIn, TOut>` class provides a default implementation of `RailwayStep` that handles error propagation.

### Input/Output Flow

1. **Input Retrieval**: When a step is executed, its input is retrieved from Memory
2. **Processing**: The step's `Run()` method processes the input
3. **Output Storage**: The output is stored in Memory by its type
4. **Error Handling**: Errors are captured and propagated through the Either monad

### Railway Oriented Programming

ChainSharp implements the Railway Oriented Programming pattern:

1. **Two Tracks**: Success (Right) and Failure (Left)
2. **Automatic Switching**: If a step fails, subsequent steps are skipped
3. **Error Propagation**: Errors are passed along the chain
4. **Final Resolution**: The `Resolve()` method returns the final result or error

## Extension Methods

### Activate

```csharp
workflow.Activate(input, otherInputs...);
```

- Initializes the workflow's Memory
- Adds the input object(s) to Memory
- Registers interfaces of the input objects
- Handles tuple inputs by extracting and storing individual elements

### Chain

```csharp
workflow.Chain<TStep>();
```

- Executes a step in the workflow
- Retrieves the step's input from Memory
- Stores the step's output in Memory
- Propagates errors through the Either monad
- Returns the workflow for method chaining

### Resolve

```csharp
workflow.Resolve();
```

- Extracts the final result from Memory
- Returns an Either<Exception, TReturn> containing the result or an error
- Can accept a specific return value or extract it by type from Memory

### Extract

```csharp
workflow.Extract<TIn, TOut>();
workflow.Extract<TIn, TOut>(input);
```

- Extracts a property or field of type TOut from an object of type TIn
- Stores the extracted value in Memory
- Useful for accessing nested properties without creating dedicated steps

### ShortCircuit

```csharp
workflow.ShortCircuit<TStep>();
```

- Similar to Chain, but sets a short-circuit value if successful
- Subsequent steps are still executed, but the final result is the short-circuit value
- Useful for early returns while still allowing cleanup steps to run

### AddServices

```csharp
workflow.AddServices(service1, service2);
```

- Adds services to the workflow's Memory
- Registers services by their interface types
- Handles mocked services by extracting the mocked interface

## Chain Method Overloads

### Primary Method: Chain<TStep>()

```csharp
workflow.Chain<TStep>();
```

This is the main method that should be used in most cases. It:
- Instantiates the step using dependency injection
- Extracts the input type from Memory
- Executes the step
- Stores the output in Memory

### Other Overloads

1. **Chain<TStep, TIn, TOut>(step, input, out output)**:
   - Explicitly provides the step instance and input
   - Captures the output in an out parameter
   - Useful for fine-grained control and output capture

2. **Chain<TStep, TIn>()**:
   - For steps that return Unit (void)
   - Simplifies the syntax for steps without meaningful output

3. **IChain<TStep>()**:
   - For interface-based steps that are already in Memory
   - Useful when steps are registered as services

The main `Chain<TStep>()` method is preferred because it:
- Requires less type information
- Handles dependency injection automatically
- Maintains a cleaner, more readable workflow definition

## Testing Workflows

### Test Setup

Tests typically:
1. Configure services using `ConfigureServices()`
2. Create a workflow instance
3. Provide input data
4. Execute the workflow
5. Assert on the results

### Example Test

```csharp
[Theory]
public async Task TestChain()
{
    // Arrange
    var prepare = ServiceProvider.GetRequiredService<IPrepare>();
    var bottle = ServiceProvider.GetRequiredService<IBottle>();
    var workflow = new ChainTest(_brew, prepare, bottle);
    var ingredients = new Ingredients() { /* ... */ };

    // Act
    var result = await workflow.Run(ingredients);

    // Assert
    workflow.Memory.Should().NotBeNull();
    workflow.Exception.Should().BeNull();
    result.Should().NotBeNull();
}
```

Errors are propagated through the Either monad:
- If a step throws an exception, it's captured in the Left track
- Subsequent steps are skipped
- The final result contains either the successful output or the exception

### Short-Circuiting

The `ShortCircuit` method allows early returns:
- If the step succeeds, its output becomes the final result
- Subsequent steps are still executed (for cleanup)
- Useful for validation or early-exit scenarios

### Tuple Handling

ChainSharp has special handling for tuples:
- Tuple elements are extracted and stored individually
- Tuple types can be reconstructed from individual elements
- Supports tuples up to 7 elements

## Best Practices

1. **Use Chain<TStep>() when possible**:
   - Simpler syntax
   - Automatic dependency injection
   - Cleaner workflow definitions

2. **Design steps with single responsibilities**:
   - Each step should do one thing well
   - Inputs and outputs should be clearly defined

3. **Use interfaces for steps**:
   - Enables dependency injection
   - Facilitates testing with mocks

4. **Leverage the Memory system**:
   - Store common objects that multiple steps need
   - Use Extract for accessing nested properties

5. **Handle errors appropriately**:
   - Let the Railway pattern handle most errors
   - Use try/catch only when necessary within steps
