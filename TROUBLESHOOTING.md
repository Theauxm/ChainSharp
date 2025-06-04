# ChainSharp Troubleshooting Guide

## Common Issues, Debugging, and Solutions

This guide helps you diagnose and resolve common issues when working with ChainSharp. Each section includes symptoms, causes, and step-by-step solutions.

## Quick Diagnostic Checklist

When you encounter issues, run through this checklist first:

1. **Dependency Injection Setup**: Are ChainSharp services registered correctly?
2. **Input Type Uniqueness**: Does each workflow have a unique input type?
3. **Effect Providers**: Are the necessary effect providers registered?
4. **Database Connection**: Is the database accessible and properly configured?
5. **Workflow Discovery**: Are workflows implementing the correct interfaces?

## Dependency Injection Issues

### Problem: Properties with [Inject] are null

#### Symptoms
```csharp
// Runtime NullReferenceException
public class MyWorkflow : EffectWorkflow<MyInput, MyOutput>
{
    [Inject]
    public IMyService MyService { get; set; } // This is null at runtime
    
    protected override async Task<Either<Exception, MyOutput>> RunInternal(MyInput input)
    {
        // Throws NullReferenceException
        return await MyService.DoSomethingAsync(input);
    }
}
```

#### Common Causes & Solutions

**Cause 1: Service not registered in DI container**
```csharp
// ❌ Problem: Service not registered
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(typeof(Program).Assembly)
);

// ✅ Solution: Register the service
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(typeof(Program).Assembly)
);
services.AddScoped<IMyService, MyService>(); // Add this line
```

**Cause 2: Workflow not properly registered**
```csharp
// ❌ Problem: Manual workflow instantiation
var workflow = new MyWorkflow(); // Properties won't be injected

// ✅ Solution: Use DI container or WorkflowBus
var workflow = serviceProvider.GetRequiredService<IMyWorkflow>();
// OR
var result = await workflowBus.RunAsync<MyOutput>(input);
```

**Cause 3: Property not marked with [Inject]**
```csharp
// ❌ Problem: Missing [Inject] attribute
public IMyService MyService { get; set; }

// ✅ Solution: Add [Inject] attribute
[Inject]
public IMyService MyService { get; set; }
```

### Problem: WorkflowBus not found

#### Symptoms
```csharp
// Exception: Unable to resolve service for type 'IWorkflowBus'
var workflowBus = serviceProvider.GetRequiredService<IWorkflowBus>();
```

#### Solution
```csharp
// Ensure WorkflowBus is registered
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(typeof(Program).Assembly) // This registers IWorkflowBus
);
```

## Workflow Discovery Issues

### Problem: "Could not find workflow with input type"

#### Symptoms
```csharp
// WorkflowException: Could not find workflow with input type (MyInput)
await workflowBus.RunAsync<MyOutput>(new MyInput());
```

#### Common Causes & Solutions

**Cause 1: Input type not unique**
```csharp
// ❌ Problem: Multiple workflows with same input type
public class CreateUserWorkflow : EffectWorkflow<UserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UserRequest, User> { }

// ✅ Solution: Make input types unique
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UpdateUserRequest, User> { }
```

**Cause 2: Workflow not in scanned assemblies**
```csharp
// ❌ Problem: Workflow in different assembly
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(typeof(Program).Assembly) // MyWorkflow not in this assembly
);

// ✅ Solution: Include all assemblies with workflows
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(
        typeof(Program).Assembly,
        typeof(MyWorkflow).Assembly // Add the assembly containing MyWorkflow
    )
);
```

**Cause 3: Workflow is abstract**
```csharp
// ❌ Problem: Abstract workflow won't be discovered
public abstract class BaseWorkflow : EffectWorkflow<MyInput, MyOutput> { }

// ✅ Solution: Make workflow concrete
public class ConcreteWorkflow : BaseWorkflow { }
```

**Cause 4: Workflow doesn't implement IEffectWorkflow<,>**
```csharp
// ❌ Problem: Doesn't implement required interface
public class MyWorkflow : Workflow<MyInput, MyOutput> { }

// ✅ Solution: Inherit from EffectWorkflow or implement interface
public class MyWorkflow : EffectWorkflow<MyInput, MyOutput> { }
```

### Problem: Workflow discovered but won't resolve

#### Symptoms
```csharp
// Exception: Unable to resolve service for type 'MyWorkflow'
```

#### Solution
```csharp
// Ensure workflow is registered with proper interface
public interface IMyWorkflow : IEffectWorkflow<MyInput, MyOutput> { }

public class MyWorkflow : EffectWorkflow<MyInput, MyOutput>, IMyWorkflow
{
    // Implementation
}

// Registration happens automatically with AddEffectWorkflowBus
```

## Database and Effect Provider Issues

### Problem: "No database provider configured"

#### Symptoms
```csharp
// Exception when saving metadata
```

#### Solution
```csharp
// Add a database effect provider
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString) // For production
        // OR
        .AddInMemoryEffect() // For testing
);
```

### Problem: Database connection failures

#### Symptoms
```csharp
// Npgsql.NpgsqlException: Connection failed
// System.Data.SqlClient.SqlException: Connection timeout
```

#### Diagnostic Steps

**Step 1: Verify connection string**
```csharp
// Test connection string format
var connectionString = "Host=localhost;Database=chainsharp;Username=user;Password=pass";

// Test connection manually
using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(); // This should not throw
```

**Step 2: Check database accessibility**
```bash
# Test from command line
psql -h localhost -U user -d chainsharp

# Check if database exists
SELECT current_database();
```

**Step 3: Verify schema setup**
```sql
-- Check if chain_sharp schema exists
SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'chain_sharp';

-- Check if tables exist
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'chain_sharp';
```

#### Solutions

**Solution 1: Fix connection string**
```csharp
// Configuration patterns
// Development
"Host=localhost;Database=chainsharp_dev;Username=dev_user;Password=dev_pass"

// Production
"Host=prod-db.company.com;Database=chainsharp;Username=app_user;Password=secure_pass;SSL Mode=Require"

// Docker
"Host=database;Database=chainsharp;Username=postgres;Password=postgres123"
```

**Solution 2: Run migrations manually**
```csharp
// Force migration in Program.cs
var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
await DatabaseMigrator.Migrate(connectionString);
```

### Problem: JSON serialization errors

#### Symptoms
```csharp
// System.Text.Json.JsonException: A possible object cycle was detected
// System.Text.Json.JsonException: The JSON value could not be converted
```

#### Solutions

**Solution 1: Configure JSON options**
```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .SaveWorkflowParameters(new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve, // Handle circular references
            WriteIndented = false, // Reduce size
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
);
```

**Solution 2: Use JsonIgnore for problematic properties**
```csharp
public class MyModel
{
    public string Name { get; set; }
    
    [JsonIgnore]
    public ComplexObject ProblematicProperty { get; set; } // Skip this
    
    [JsonIgnore]
    public event EventHandler SomeEvent; // Skip events
}
```

**Solution 3: Create DTOs for serialization**
```csharp
// Instead of serializing domain objects directly
public record CreateUserRequest
{
    public string Email { get; init; }
    public string Name { get; init; }
    // Only include serializable properties
}
```

## Memory Leak Issues

### Problem: Memory usage grows over time

#### Diagnostic Steps

**Step 1: Enable memory profiling**
```csharp
// In test projects, use MemoryProfiler
public class MemoryLeakTest
{
    [Fact]
    public async Task Workflow_DoesNotLeak_Memory()
    {
        var initialMemory = MemoryProfiler.GetCurrentMemoryUsage();
        
        // Run workflow multiple times
        for (int i = 0; i < 100; i++)
        {
            await workflowBus.RunAsync<MyOutput>(new MyInput());
        }
        
        // Force garbage collection
        MemoryProfiler.ForceGarbageCollection();
        
        var finalMemory = MemoryProfiler.GetCurrentMemoryUsage();
        var memoryIncrease = finalMemory - initialMemory;
        
        // Assert memory didn't grow significantly
        memoryIncrease.Should().BeLessThan(10_000_000); // 10MB threshold
    }
}
```

**Step 2: Check for common leak sources**

#### Common Causes & Solutions

**Cause 1: Event subscriptions not cleaned up**
```csharp
// ❌ Problem: Event not unsubscribed
public class LeakyStep : Step<Input, Output>
{
    public override async Task<Output> Run(Input input)
    {
        SomeService.EventOccurred += HandleEvent; // Leak!
        return await ProcessAsync(input);
    }
}

// ✅ Solution: Implement IDisposable
public class CleanStep : Step<Input, Output>, IDisposable
{
    public override async Task<Output> Run(Input input)
    {
        SomeService.EventOccurred += HandleEvent;
        return await ProcessAsync(input);
    }
    
    public void Dispose()
    {
        SomeService.EventOccurred -= HandleEvent;
    }
}
```

**Cause 2: Static references to workflows**
```csharp
// ❌ Problem: Static reference prevents GC
public static MyWorkflow CachedWorkflow = new MyWorkflow();

// ✅ Solution: Use DI container instead
// Let the DI container manage lifecycle
services.AddTransient<IMyWorkflow, MyWorkflow>();
```

**Cause 3: Large objects held in memory**
```csharp
// ❌ Problem: Large object held in workflow field
public class DataProcessingWorkflow : EffectWorkflow<Input, Output>
{
    private byte[] largeData; // This stays in memory
    
    protected override async Task<Either<Exception, Output>> RunInternal(Input input)
    {
        largeData = await LoadLargeDataAsync();
        return await ProcessDataAsync(largeData);
    }
}

// ✅ Solution: Use local variables and dispose
public class DataProcessingWorkflow : EffectWorkflow<Input, Output>
{
    protected override async Task<Either<Exception, Output>> RunInternal(Input input)
    {
        var largeData = await LoadLargeDataAsync();
        try
        {
            return await ProcessDataAsync(largeData);
        }
        finally
        {
            // Explicitly clear reference
            largeData = null;
        }
    }
}
```

### Problem: EffectProvider memory leaks

#### Symptoms
```csharp
// Memory usage grows with each workflow execution
```

#### Solution: Implement proper disposal
```csharp
public class ProperEffectProvider : IEffectProvider
{
    private readonly Dictionary<IModel, string> _trackedModels = new();
    private readonly HttpClient _httpClient;
    
    public ProperEffectProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task Track(IModel model) { /* implementation */ }
    
    public async Task SaveChanges(CancellationToken cancellationToken) 
    { 
        // Clear tracked models after saving
        _trackedModels.Clear();
    }
    
    public void Dispose()
    {
        _trackedModels.Clear();
        _httpClient?.Dispose();
    }
}
```

## Performance Issues

### Problem: Slow workflow execution

#### Diagnostic Steps

**Step 1: Enable timing logs**
```csharp
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .AddEffectDataContextLogging(LogLevel.Information) // Enable SQL logging
);
```

**Step 2: Profile each component**
```csharp
public class ProfiledWorkflow : EffectWorkflow<Input, Output>
{
    [Inject]
    public ILogger<ProfiledWorkflow> Logger { get; set; }
    
    protected override async Task<Either<Exception, Output>> RunInternal(Input input)
    {
        using var activity = Activity.StartActivity("ProcessWorkflow");
        
        Logger.LogInformation("Starting workflow with input: {Input}", input);
        var stopwatch = Stopwatch.StartNew();
        
        var result = await Activate(input)
            .Chain<TimedValidationStep>()
            .Chain<TimedProcessingStep>()
            .Chain<TimedNotificationStep>()
            .Resolve();
            
        stopwatch.Stop();
        Logger.LogInformation("Workflow completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        
        return result;
    }
}
```

#### Common Performance Issues

**Issue 1: N+1 database queries**
```csharp
// ❌ Problem: Multiple database calls
public class SlowStep : Step<OrderList, ProcessedOrderList>
{
    public override async Task<ProcessedOrderList> Run(OrderList input)
    {
        var results = new List<ProcessedOrder>();
        
        foreach (var order in input.Orders)
        {
            // This creates N database calls!
            var customer = await CustomerRepository.GetByIdAsync(order.CustomerId);
            results.Add(new ProcessedOrder { Order = order, Customer = customer });
        }
        
        return new ProcessedOrderList { Orders = results };
    }
}

// ✅ Solution: Batch database calls
public class FastStep : Step<OrderList, ProcessedOrderList>
{
    public override async Task<ProcessedOrderList> Run(OrderList input)
    {
        // Single database call
        var customerIds = input.Orders.Select(o => o.CustomerId).Distinct();
        var customers = await CustomerRepository.GetByIdsAsync(customerIds);
        var customerDict = customers.ToDictionary(c => c.Id);
        
        var results = input.Orders.Select(order => new ProcessedOrder
        {
            Order = order,
            Customer = customerDict[order.CustomerId]
        }).ToList();
        
        return new ProcessedOrderList { Orders = results };
    }
}
```

**Issue 2: Unnecessary effect providers**
```csharp
// ❌ Problem: Too many effects in production
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)
        .AddJsonEffect()                    // Remove in production
        .SaveWorkflowParameters()           // Consider removing if not needed
        .AddEffectWorkflowBus(assemblies)
);

// ✅ Solution: Environment-specific configuration
if (env.IsDevelopment())
{
    options.AddJsonEffect();
}

if (requiresParameterTracking)
{
    options.SaveWorkflowParameters();
}
```

**Issue 3: Large object serialization**
```csharp
// ❌ Problem: Serializing large objects
public record ProcessFileRequest
{
    public byte[] FileContents { get; init; } // Large data
    public string FileName { get; init; }
}

// ✅ Solution: Use references instead
public record ProcessFileRequest
{
    public string FileId { get; init; }      // Reference to stored file
    public string FileName { get; init; }
}
```

## Testing Issues

### Problem: Tests are flaky or inconsistent

#### Common Causes & Solutions

**Cause 1: Shared database state**
```csharp
// ❌ Problem: Tests affecting each other
[Fact]
public async Task Test1() 
{
    // Uses shared database, affects Test2
}

[Fact]
public async Task Test2() 
{
    // Depends on state from Test1
}

// ✅ Solution: Use isolated databases
public class TestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    
    public TestFixture()
    {
        ServiceProvider = new ServiceCollection()
            .AddChainSharpEffects(options => 
                options.AddInMemoryEffect() // Each test gets fresh database
            )
            .BuildServiceProvider();
    }
}
```

**Cause 2: Async timing issues**
```csharp
// ❌ Problem: Not awaiting async operations
[Fact]
public void TestWorkflow() // Missing async
{
    var result = workflowBus.RunAsync<Output>(input); // Not awaited
    Assert.True(result.IsCompleted); // Flaky!
}

// ✅ Solution: Proper async/await
[Fact]
public async Task TestWorkflow()
{
    var result = await workflowBus.RunAsync<Output>(input);
    Assert.NotNull(result);
}
```

## Debugging Tips

### Enable Comprehensive Logging

```csharp
services.AddLogging(builder => 
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug)
        .AddFilter("ChainSharp", LogLevel.Trace) // Detailed ChainSharp logs
        .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information) // SQL logs
);
```

### Use Debugger Breakpoints in Steps

```csharp
public class DebuggableStep : Step<Input, Output>
{
    public override async Task<Either<Exception, Output>> Run(Input input)
    {
        // Set breakpoint here to inspect input
        System.Diagnostics.Debugger.Break(); // Only in debug builds
        
        var result = await ProcessInputAsync(input);
        
        // Set breakpoint here to inspect result
        return result;
    }
}
```

### Inspect Metadata in Tests

```csharp
[Fact]
public async Task DebugWorkflowMetadata()
{
    var result = await workflowBus.RunAsync<Output>(input);
    
    // Inspect metadata
    using var dataContext = (IDataContext)dataContextFactory.Create();
    var metadata = await dataContext.Metadatas
        .Include(m => m.Logs)
        .FirstAsync(m => m.Name == nameof(MyWorkflow));
    
    // Output metadata for debugging
    Console.WriteLine($"Workflow State: {metadata.WorkflowState}");
    Console.WriteLine($"Input: {metadata.Input}");
    Console.WriteLine($"Output: {metadata.Output}");
    
    if (metadata.FailureException != null)
    {
        Console.WriteLine($"Error: {metadata.FailureException}");
        Console.WriteLine($"Stack Trace: {metadata.StackTrace}");
    }
}
```

This troubleshooting guide covers the most common issues you'll encounter with ChainSharp. When in doubt, start with the diagnostic checklist and work through the relevant sections systematically.
