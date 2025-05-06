# ChainSharp.Effect.Mediator Architecture

ChainSharp.Effect.Mediator provides a mediator pattern implementation for ChainSharp.Effect workflows, enabling automatic workflow discovery, registration, and execution based on input types. This document explains the architecture, key components, and usage patterns of this powerful extension.

## Introduction

The Mediator project implements a mediator pattern for ChainSharp.Effect workflows, providing several key capabilities:

1. **Automatic Workflow Discovery**: Scans assemblies to find all EffectWorkflow implementations
2. **Input-Based Routing**: Maps input types to their corresponding workflows
3. **Automatic Dependency Injection**: Ensures all [Inject] attributes are properly resolved
4. **Simplified Invocation**: Allows executing workflows by providing just the input
5. **Nested Workflow Support**: Enables parent-child workflow relationships

This approach significantly simplifies the use of workflows in applications by removing the need to manually wire up workflows and their dependencies.

## Core Components

### WorkflowRegistry

The `WorkflowRegistry` is responsible for discovering workflows and mapping input types to workflow types:

```csharp
public class WorkflowRegistry : IWorkflowRegistry
{
    public Dictionary<Type, Type> InputTypeToWorkflow { get; set; }

    public WorkflowRegistry(params Assembly[] assemblies)
    {
        // The type we will be looking for in our assemblies
        var workflowType = typeof(IEffectWorkflow<,>);

        var allWorkflowTypes = new HashSet<Type>();

        foreach (var assembly in assemblies)
        {
            var workflowTypes = assembly
                .GetTypes()
                .Where(x => x.IsClass)
                .Where(x => x.IsAbstract == false)
                .Where(
                    x =>
                        x.GetInterfaces()
                            .Where(y => y.IsGenericType)
                            .Select(y => y.GetGenericTypeDefinition())
                            .Contains(workflowType)
                )
                .Select(
                    x =>
                        // Prefer to inject via interface, but if it doesn't exist then inject by underlying type
                        x.GetInterfaces().FirstOrDefault(y => y.IsGenericType == false) ?? x
                );

            allWorkflowTypes.UnionWith(workflowTypes);
        }

        InputTypeToWorkflow = allWorkflowTypes.ToDictionary(
            x =>
                x.GetInterfaces()
                    .Where(interfaceType => interfaceType.IsGenericType)
                    .FirstOrDefault(
                        interfaceType => interfaceType.GetGenericTypeDefinition() == workflowType
                    )
                    ?.GetGenericArguments()
                    .FirstOrDefault()
                ?? throw new WorkflowException(
                    $"Could not find an interface and/or an inherited interface of type ({workflowType.Name}) on target type ({x.Name}) with FullName ({x.FullName}) on Assembly ({x.AssemblyQualifiedName})."
                )
        );
    }
}
```

Key aspects:
- Scans provided assemblies for classes that implement `IEffectWorkflow<,>`
- Filters out abstract classes
- Extracts the input type from the workflow's generic interface
- Creates a dictionary mapping input types to workflow types
- Prefers non-generic interfaces for workflow registration

### WorkflowBus

The `WorkflowBus` is the central component that executes workflows based on their input:

```csharp
public class WorkflowBus(IServiceProvider _serviceProvider, IWorkflowRegistry _registryService) : IWorkflowBus
{
    public Task<TOut> RunAsync<TOut>(object workflowInput, Metadata? metadata = null)
    {
        // Find the workflow type for this input
        var inputType = workflowInput.GetType();
        var foundWorkflow = _registryService.InputTypeToWorkflow.TryGetValue(
            inputType,
            out var workflowType
        );

        if (!foundWorkflow || workflowType == null)
            throw new WorkflowException(
                $"Could not find workflow with input type ({inputType.Name})"
            );

        // Resolve the workflow from the service provider
        var workflowService = _serviceProvider.GetRequiredService(workflowType);
        
        // Inject properties marked with [Inject]
        _serviceProvider.InjectProperties(workflowService);

        // Set parent metadata if provided
        if (metadata != null)
        {
            var parentIdProperty = workflowService
                .GetType()
                .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .First(x => x.Name == "ParentId");

            parentIdProperty.SetValue(workflowService, metadata.Id);
        }

        // Get and invoke the Run method
        var runMethod = workflowService
            .GetType()
            .GetMethods()
            .Where(x => x.Name == "Run")
            .Where(x => x.GetParameters().Length == 1)
            .First(x => x.Module.Name.Contains("Effect"));

        var taskRunMethod = (Task<TOut>?)runMethod.Invoke(workflowService, [workflowInput]);

        if (taskRunMethod == null)
            throw new WorkflowException(
                $"Failed to invoke Run method for workflow type ({workflowService.GetType().Name})"
            );

        return taskRunMethod;
    }
}
```

Key aspects:
- Uses the WorkflowRegistry to find the appropriate workflow for a given input
- Resolves the workflow from the service provider
- Injects properties marked with [Inject] using the InjectProperties extension method
- Sets up parent-child relationships for nested workflows
- Uses reflection to invoke the Run method with the input
- Returns the result as a strongly-typed Task

## Workflow Discovery Process

The workflow discovery process happens when the WorkflowRegistry is created:

1. **Assembly Scanning**: Each provided assembly is scanned for types
2. **Type Filtering**:
   - Must be a class (not an interface or struct)
   - Must not be abstract
   - Must implement IEffectWorkflow<,>
3. **Interface Selection**:
   - Prefers non-generic interfaces for service registration
   - Falls back to the first interface or the class itself
4. **Input Type Extraction**:
   - Finds the IEffectWorkflow<,> interface
   - Extracts the first generic argument (TIn)
5. **Dictionary Creation**:
   - Creates a mapping from input types to workflow types
   - This dictionary is used by the WorkflowBus to find workflows

This process happens once at startup, creating a cached mapping that is used for all subsequent workflow executions.

## Key Constraints

### Input Type Uniqueness

**Each input type must be unique across all workflows.**

This is a fundamental constraint of the mediator pattern implementation. Since the WorkflowBus uses the input type to determine which workflow to execute, having multiple workflows that accept the same input type would create ambiguity.

For example, if both `CreateUserWorkflow` and `RegisterUserWorkflow` accepted a `UserRegistrationRequest` input, the WorkflowBus wouldn't know which one to execute when given that input.

To address this:
- Design input types that are specific to each workflow
- Use inheritance or composition to share common properties
- Consider using marker interfaces or wrapper classes for disambiguation

### Non-Abstract Requirement

**Workflow classes cannot be abstract.**

The WorkflowRegistry explicitly filters out abstract classes during the discovery process. This is because:
- Abstract classes cannot be instantiated
- They are typically meant as base classes for other workflows
- Including them would create mappings that can't be resolved

If you need to share functionality across workflows, consider:
- Using composition instead of inheritance
- Creating abstract base classes but not implementing IEffectWorkflow<,> on them
- Using extension methods or utility classes

### Interface Requirements

**Workflows should implement at least one interface.**

While not strictly required (the class itself will be used as a fallback), the WorkflowRegistry is designed to work best with interfaces:
- It prefers to register workflows by their non-generic interface
- This enables proper dependency injection and mocking
- It allows for better separation of concerns

The recommended pattern is:
```csharp
public interface ICreateUserWorkflow : IEffectWorkflow<CreateUserRequest, User> { }

public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User>, ICreateUserWorkflow
{
    // Implementation
}
```

## Dependency Injection

### Property Injection

The WorkflowBus ensures that all properties marked with the `[Inject]` attribute are properly injected:

```csharp
serviceProvider.InjectProperties(workflowService);
```

This calls the `InjectProperties` extension method from ChainSharp.Effect:

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

This means that any property marked with `[Inject]` will be automatically populated from the service provider, without needing to pass it through the constructor.

### Workflow Registration

Workflows are registered with the dependency injection container using the `RegisterEffectWorkflows` method:

```csharp
public static IServiceCollection RegisterEffectWorkflows(
    this IServiceCollection services,
    ServiceLifetime serviceLifetime = ServiceLifetime.Transient,
    params Assembly[] assemblies
)
{
    // Discover workflows
    var workflowType = typeof(IEffectWorkflow<,>);
    var types = new List<(Type, Type)>();
    
    // For each assembly, find workflow types
    foreach (var assembly in assemblies)
    {
        var workflowTypes = assembly
            .GetTypes()
            .Where(x => x.IsClass)
            .Where(x => x.IsAbstract == false)
            .Where(
                x =>
                    x.GetInterfaces()
                        .Where(y => y.IsGenericType)
                        .Select(y => y.GetGenericTypeDefinition())
                        .Contains(workflowType)
            )
            .Select(
                type =>
                    (
                        type.GetInterfaces().FirstOrDefault(y => y.IsGenericType == false)
                            ?? type.GetInterfaces().FirstOrDefault()
                            ?? throw new WorkflowException(
                                $"Could not find an interface attached to ({type.Name}) with Full Name ({type.FullName}) on Assembly ({type.AssemblyQualifiedName}). At least one Interface is required."
                            ),
                        type
                    )
            );

        types.AddRange(workflowTypes);
    }

    // Register each workflow with the appropriate lifetime
    foreach (var (typeInterface, typeImplementation) in types)
    {
        switch (serviceLifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingletonChainSharpWorkflow(typeInterface, typeImplementation);
                break;
            case ServiceLifetime.Scoped:
                services.AddScopedChainSharpWorkflow(typeInterface, typeImplementation);
                break;
            case ServiceLifetime.Transient:
                services.AddTransientChainSharpWorkflow(typeInterface, typeImplementation);
                break;
        }
    }

    return services;
}
```

This method:
1. Scans assemblies for workflow types
2. Extracts the interface and implementation types
3. Registers each workflow with the specified lifetime
4. Uses the specialized ChainSharp registration methods that handle property injection

## Usage Examples

### Basic Registration

To register the WorkflowBus and discover workflows in your application:

```csharp
// In Program.cs or Startup.cs
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(
        assemblies: [typeof(Program).Assembly]
    )
);
```

This will:
1. Scan the specified assemblies for workflows
2. Register the WorkflowRegistry with the discovered workflows
3. Register the WorkflowBus as a scoped service
4. Register all discovered workflows with the specified lifetime (default: Transient)

### Controller Example

Using the WorkflowBus in an ASP.NET Core controller:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(IWorkflowBus _workflowBus) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        try
        {
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

This pattern:
- Injects the IWorkflowBus into the controller
- Uses it to execute workflows based on the request
- Handles exceptions appropriately
- Returns the workflow result to the client

### Nested Workflow Example

Using the WorkflowBus to execute nested workflows:

```csharp
public class ProcessOrderWorkflow : EffectWorkflow<ProcessOrderRequest, OrderResult>, IProcessOrderWorkflow
{
    [Inject]
    public IWorkflowBus WorkflowBus { get; set; }
    
    protected override async Task<Either<Exception, OrderResult>> RunInternal(ProcessOrderRequest input)
    {
        // First validate the order
        var validationResult = await WorkflowBus.RunAsync<ValidationResult>(
            new ValidateOrderRequest { Order = input.Order },
            Metadata // Pass the current workflow's metadata as the parent
        );
        
        if (!validationResult.IsValid)
            return new OrderException(validationResult.Errors);
            
        // Then process the payment
        var paymentResult = await WorkflowBus.RunAsync<PaymentResult>(
            new ProcessPaymentRequest { 
                OrderId = input.Order.Id,
                Amount = input.Order.Total,
                PaymentMethod = input.PaymentMethod
            },
            Metadata
        );
        
        if (!paymentResult.Success)
            return new OrderException("Payment failed: " + paymentResult.ErrorMessage);
            
        // Finally fulfill the order
        var fulfillmentResult = await WorkflowBus.RunAsync<FulfillmentResult>(
            new FulfillOrderRequest { OrderId = input.Order.Id },
            Metadata
        );
        
        return new OrderResult {
            OrderId = input.Order.Id,
            Status = fulfillmentResult.Status,
            EstimatedDelivery = fulfillmentResult.EstimatedDelivery
        };
    }
}
```

This pattern:
- Injects the IWorkflowBus into the workflow
- Uses it to execute child workflows
- Passes the current workflow's metadata to establish the parent-child relationship
- Combines the results of multiple workflows into a single result

## Best Practices

### Workflow Design

1. **Single Responsibility**:
   - Each workflow should have a clear, focused purpose
   - Break complex processes into multiple workflows

2. **Interface-Based**:
   - Always define an interface for your workflows
   - Follow the naming convention: IXxxWorkflow

3. **Input/Output Types**:
   - Create specific input types for each workflow
   - Use descriptive names that reflect the workflow's purpose
   - Include all necessary data in the input
   - Keep output types focused on the result

4. **Error Handling**:
   - Use the Railway pattern (Either<Exception, TOut>) for error handling
   - Return specific exception types for different error conditions
   - Include meaningful error messages

### Input Type Design

1. **Uniqueness**:
   - Ensure each workflow has a unique input type
   - Use wrapper classes if needed to disambiguate

2. **Immutability**:
   - Design input types as immutable records or classes
   - Use required properties to ensure all necessary data is provided

3. **Validation**:
   - Include validation logic in the workflow
   - Consider using a validation library like FluentValidation

4. **Naming Conventions**:
   - Use descriptive names that reflect the workflow's purpose
   - Follow a consistent pattern: XxxRequest or XxxInput

### Performance Considerations

1. **Service Lifetime**:
   - Use Transient lifetime for most workflows
   - Use Scoped lifetime for workflows that need to share state within a request
   - Avoid Singleton lifetime unless the workflow is truly stateless

2. **Assembly Scanning**:
   - Only include assemblies that contain workflows
   - Consider using marker interfaces or assembly attributes to limit scanning

3. **Caching**:
   - The WorkflowRegistry caches the mapping of input types to workflow types
   - This happens once at startup, so there's no runtime performance penalty

4. **Reflection**:
   - The WorkflowBus uses reflection to invoke the Run method
   - This has a small performance cost, but it's negligible compared to the actual workflow execution

## Conclusion

ChainSharp.Effect.Mediator provides a powerful, flexible way to organize and execute workflows in your application. By automatically discovering workflows and routing inputs to the appropriate implementation, it simplifies the development of complex business processes while maintaining separation of concerns and testability.

The key constraint to remember is that each input type must be unique across all workflows, as this is how the WorkflowBus determines which workflow to execute. With this in mind, you can build scalable, maintainable applications using the mediator pattern for workflow execution.
