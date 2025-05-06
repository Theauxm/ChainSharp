# ChainSharp.Effect Architecture

ChainSharp.Effect is an abstraction layer built on top of the core ChainSharp library, providing enhanced capabilities for dependency injection, effect tracking, and workflow execution. This document explains the architecture, key components, and usage patterns of ChainSharp.Effect.

## Introduction

ChainSharp.Effect extends the Railway Oriented Programming model of ChainSharp with additional features:

- **Automatic Dependency Injection**: Services are automatically injected into workflows using attributes
- **Effect Pattern**: Operations are tracked and executed as effects with deferred execution
- **Metadata Tracking**: Workflow execution is tracked with detailed metadata
- **Extensibility**: Base abstractions for implementing various effects (database, JSON, mediator, etc.)

## Core Concepts

### Automatic Dependency Injection

Unlike the core ChainSharp library where services must be manually added to workflows using the `.AddServices()` method, ChainSharp.Effect provides automatic dependency injection through the `InjectAttribute`:

```csharp
public class MyWorkflow : EffectWorkflow<MyInput, MyOutput>
{
    [Inject]
    public IMyService MyService { get; set; }
    
    [Inject]
    public ILogger<MyWorkflow> Logger { get; set; }
    
    [Inject]
    public IEffectRunner EffectRunner { get; set; }
    
    protected override async Task<Either<Exception, MyOutput>> RunInternal(MyInput input)
    {
        // Use injected services directly
        return Activate(input)
            .Chain<MyStep>()
            .Resolve();
    }
}
```

The `InjectAttribute` marks properties for automatic injection when the workflow is resolved from the service provider. This eliminates the need to pass services through constructors or use the `.AddServices()` method.

### Effect Pattern

The Effect pattern in ChainSharp.Effect is inspired by the Unit of Work pattern and is similar to how Entity Framework's DbContext works:

1. **Track**: Effects are tracked but not immediately executed
2. **SaveChanges**: Tracked effects are executed when SaveChanges is called
3. **Providers**: Different effect providers can implement specific behaviors

This pattern allows for:
- Batching operations
- Transaction-like semantics
- Consistent execution model across different effect types

### EffectWorkflow

`EffectWorkflow<TIn, TOut>` extends the base `Workflow<TIn, TOut>` class with:

- Automatic dependency injection
- Metadata tracking
- Integration with the EffectRunner
- Error handling and logging

The EffectWorkflow handles the lifecycle of workflow execution, including initialization, execution, and finalization with proper metadata tracking.

### EffectRunner

The `EffectRunner` is responsible for:

- Managing effect providers
- Tracking models through providers
- Coordinating SaveChanges operations across providers
- Disposing providers when done

## Architecture Components

### Component Diagram

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  EffectWorkflow │────▶│   EffectRunner  │────▶│  EffectProvider │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                      │                        │
         │                      │                        │
         ▼                      ▼                        ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    Metadata     │     │ ProviderFactory │     │      Model      │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

### Workflow Execution Flow

1. Client code calls `workflow.Run(input)`
2. EffectWorkflow initializes metadata and starts tracking
3. Base Workflow executes the chain of steps
4. Effects are tracked but not executed during step execution
5. After all steps complete, EffectWorkflow calls `EffectRunner.SaveChanges()`
6. EffectRunner executes all tracked effects through their providers
7. Metadata is updated with the final state and result

### EffectWorkflow

The `EffectWorkflow<TIn, TOut>` class is the central component that:

1. Overrides the base `Run` method to add metadata tracking
2. Automatically receives injected services via properties with `[Inject]`
3. Initializes and finalizes workflow execution with proper metadata
4. Handles exceptions and logs errors
5. Coordinates with the EffectRunner for effect execution

Key properties that are automatically injected:

```csharp
[Inject]
public IEffectRunner? EffectRunner { get; set; }

[Inject]
public ILogger<EffectWorkflow<TIn, TOut>>? EffectLogger { get; set; }

[Inject]
public IServiceProvider? ServiceProvider { get; set; }
```

### EffectRunner

The `EffectRunner` manages a collection of effect providers and coordinates their execution:

```csharp
public class EffectRunner : IEffectRunner
{
    private List<IEffectProvider> ActiveEffectProviders { get; init; }

    public EffectRunner(IEnumerable<IEffectProviderFactory> effectProviderFactories)
    {
        ActiveEffectProviders = [];
        ActiveEffectProviders.AddRange(effectProviderFactories.RunAll(factory => factory.Create()));
    }

    public async Task SaveChanges(CancellationToken cancellationToken)
    {
        await ActiveEffectProviders.RunAllAsync(
            provider => provider.SaveChanges(cancellationToken)
        );
    }

    public async Task Track(IModel model)
    {
        ActiveEffectProviders.RunAll(provider => provider.Track(model));
    }

    public void Dispose() => DeactivateProviders();
}
```

The EffectRunner:
- Is created with a collection of provider factories
- Creates and manages effect providers
- Tracks models through all providers
- Executes SaveChanges on all providers
- Disposes providers when done

### Effect Providers

Effect providers implement the `IEffectProvider` interface:

```csharp
public interface IEffectProvider : IDisposable
{
    public Task SaveChanges(CancellationToken cancellationToken);
    public Task Track(IModel model);
}
```

Each provider:
- Tracks models of interest
- Implements specific behavior for SaveChanges
- Manages its own resources and cleanup

Providers are created by factories that implement `IEffectProviderFactory`:

```csharp
public interface IEffectProviderFactory
{
    public IEffectProvider Create();
}
```

## Models and Metadata Tracking

### The Models Folder

The `Models` folder in ChainSharp.Effect contains the data structures that represent the core domain objects used by the framework:

- **IModel**: The base interface for all models that can be tracked by effect providers
- **Metadata**: Detailed information about workflow execution
- **Log**: Records of operations and events during workflow execution
- **WorkflowExceptionData**: Structured representation of exceptions

These models form the foundation of ChainSharp.Effect's tracking and auditing capabilities.

### Metadata Model

The `Metadata` class is a central component that tracks detailed information about each workflow execution:

```csharp
public class Metadata : IMetadata
{
    public int Id { get; private set; }
    public int? ParentId { get; set; }
    public string ExternalId { get; set; }
    public string Name { get; set; }
    public string? Executor { get; private set; }
    public WorkflowState WorkflowState { get; set; }
    public string? FailureStep { get; private set; }
    public string? FailureException { get; private set; }
    public string? FailureReason { get; private set; }
    public string? StackTrace { get; set; }
    public JsonDocument? Input { get; set; }
    public JsonDocument? Output { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsChild => ParentId is not null;
    
    // Navigation properties
    public Metadata Parent { get; private set; }
    public ICollection<Metadata> Children { get; private set; }
    public ICollection<Log.Log> Logs { get; private set; }
}
```

Each `EffectWorkflow` execution creates a `Metadata` record that captures:

1. **Basic Information**:
   - Workflow name (the class name of the workflow)
   - Execution timestamps (start and end times)
   - Unique identifiers (ID and ExternalID)

2. **Execution State**:
   - Current state (Pending, InProgress, Completed, Failed)
   - Execution duration (calculated from StartTime and EndTime)

3. **Input/Output Data**:
   - Serialized input parameters
   - Serialized output result

4. **Error Information** (when failures occur):
   - The step where the failure occurred
   - Exception type and message
   - Detailed stack trace
   - Failure reason in a human-readable format

5. **Hierarchical Relationships**:
   - Parent-child relationships for nested workflows
   - Links to related log entries

### Workflow-Metadata Interaction

When an `EffectWorkflow` is executed:

1. **Initialization**: The workflow creates a new `Metadata` record in the `InitializeWorkflow` method:
   ```csharp
   private async Task<Metadata> InitializeWorkflow(TIn input)
   {
       var metadata = Metadata.Create(
           new CreateMetadata
           {
               Name = WorkflowName,
               Input = input,
               ParentId = ParentId
           }
       );
       await EffectRunner.Track(metadata);
       metadata.WorkflowState = WorkflowState.InProgress;
       return metadata;
   }
   ```

2. **Execution**: The workflow updates the metadata as it progresses:
   - State changes from Pending to InProgress
   - Input is serialized and stored

3. **Completion**: The workflow finalizes the metadata in the `FinishWorkflow` method:
   ```csharp
   private async Task<Unit> FinishWorkflow(Either<Exception, TOut> result)
   {
       var failureReason = result.IsRight ? null : result.Swap().ValueUnsafe();
       var resultState = result.IsRight ? WorkflowState.Completed : WorkflowState.Failed;
       
       Metadata.WorkflowState = resultState;
       Metadata.EndTime = DateTime.UtcNow;
       
       if (failureReason != null)
           Metadata.AddException(failureReason);
           
       return Unit.Default;
   }
   ```
   - State changes to Completed or Failed
   - End time is recorded
   - Output or exception details are stored

### Metadata Usage

The detailed metadata captured for each workflow execution enables:

1. **Monitoring and Observability**:
   - Track workflow execution times and success rates
   - Identify performance bottlenecks
   - Monitor system health

2. **Debugging and Troubleshooting**:
   - Pinpoint exactly where failures occur
   - Access detailed error information
   - Trace execution paths through nested workflows

3. **Auditing and Compliance**:
   - Record who executed workflows and when
   - Document inputs and outputs for sensitive operations
   - Maintain a complete history of system activities

4. **Analytics and Reporting**:
   - Analyze workflow performance trends
   - Generate reports on system usage
   - Identify opportunities for optimization

The metadata model is designed to be persisted to a database but can be used with any storage mechanism that implements the appropriate effect provider.

## Service Provider Integration

### Registration

Services and workflows are registered with extension methods:

```csharp
// Add ChainSharp.Effect core services
services.AddChainSharpEffects(options => 
    options.AddEffect<MyCustomEffectProviderFactory>()
);

// Register workflows
services.AddTransientChainSharpWorkflow<IMyWorkflow, MyWorkflow>();
```

The registration process:
1. Registers effect providers and factories
2. Sets up the EffectRunner
3. Configures workflow registration with dependency injection

### Dependency Injection

The key to automatic dependency injection is the `InjectProperties` extension method:

```csharp
public static void InjectProperties(this IServiceProvider serviceProvider, object instance)
{
    var properties = instance
        .GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.IsDefined(typeof(InjectAttribute)) && p.CanWrite);

    foreach (var property in properties)
    {
        if (property.GetValue(instance) != null)
            continue;

        var propertyType = property.PropertyType;
        object? service = serviceProvider.GetService(propertyType);

        if (service != null)
        {
            property.SetValue(instance, service);
        }
    }
}
```

This method:
1. Finds all properties with the `[Inject]` attribute
2. Resolves services from the service provider
3. Sets the property values on the instance

The workflow registration methods use this to inject services:

```csharp
services.AddTransient<TService>(sp =>
{
    var instance = sp.GetRequiredService<TImplementation>();
    sp.InjectProperties(instance);
    return instance;
});
```

## Extensibility

ChainSharp.Effect is designed as a foundation that can be extended to support various effect types and implementations. The core project provides the interfaces and base classes needed to create extensions, but does not implement specific effect types itself.

### Extension Points

The main extension points in ChainSharp.Effect are:

1. **Effect Providers**: By implementing the `IEffectProvider` interface, extensions can create custom effect types that track and process specific kinds of operations.

2. **Effect Provider Factories**: The `IEffectProviderFactory` interface allows for the creation of provider instances with appropriate configuration.

3. **Models**: Extensions can create custom model types that implement the `IModel` interface to be tracked by effect providers.

These extension points enable the creation of specialized implementations for various use cases while maintaining a consistent programming model.


## Usage Examples

### Creating an EffectWorkflow

```csharp
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>
{
    [Inject]
    public IUserRepository UserRepository { get; set; }
    
    [Inject]
    public ILogger<CreateUserWorkflow> Logger { get; set; }
    
    protected override async Task<Either<Exception, User>> RunInternal(CreateUserRequest input)
        => Activate(input)
            .Chain<ValidateUserStep>()
            .Chain<CreateUserStep>()
            .Chain<SendWelcomeEmailStep>()
            .Resolve();
}
```

### Registering Services

```csharp
// In Program.cs or Startup.cs
services.AddChainSharpEffects(options => 
    options.AddEffect<MyCustomEffectProviderFactory>()
);

services.AddTransientChainSharpWorkflow<ICreateUserWorkflow, CreateUserWorkflow>();
services.AddScoped<IUserRepository, UserRepository>();
```

## Best Practices

### When to Use ChainSharp.Effect

Use ChainSharp.Effect when:
- You need automatic dependency injection
- You want metadata tracking for workflows
- You're working with databases or other external systems
- You want a consistent pattern for effect execution

Use core ChainSharp when:
- You need a lightweight solution
- You don't need dependency injection
- You're not working with external systems
- You don't need metadata tracking

### Designing Workflows

- Keep workflows focused on orchestration, not implementation
- Use steps for specific operations
- Leverage the effect pattern for external operations
- Take advantage of automatic dependency injection

### Error Handling

- Let the Railway pattern handle most errors
- Use workflow metadata for detailed error tracking
- Leverage the EffectLogger for logging

## Conclusion

ChainSharp.Effect provides a powerful abstraction on top of ChainSharp, adding automatic dependency injection, effect tracking, and metadata management. It serves as a foundation for building complex workflows with consistent patterns for external operations, error handling, and service integration.
