---
layout: default
title: Troubleshooting
nav_order: 6
---

# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when working with ChainSharp.

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

## Database Issues

### Problem: Database connection failures

#### Symptoms

```
Npgsql.NpgsqlException: Failed to connect to localhost:5432
```

#### Solutions

**Check connection string:**

```csharp
// Ensure connection string is correct
services.AddChainSharpEffects(options => 
    options.AddPostgresEffect("Host=localhost;Database=chain_sharp;Username=user;Password=pass")
);
```

**Verify database is running:**

```bash
# Check if PostgreSQL is running
docker ps | grep postgres

# Or start it
docker-compose up -d database
```

### Problem: Migration errors

#### Symptoms

```
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while updating the entries
```

#### Solution

```bash
# Apply pending migrations
dotnet ef database update --project ChainSharp.Effect.Data.Postgres

# Or create a new migration if schema changed
dotnet ef migrations add MigrationName --project ChainSharp.Effect.Data.Postgres
```

## Effect Provider Issues

### Problem: Effects not being saved

#### Symptoms

- Metadata not appearing in database
- JSON logs not being written
- Parameters not being serialized

#### Common Causes & Solutions

**Cause 1: Effect provider not registered**

```csharp
// ❌ Problem: No effect providers registered
services.AddChainSharpEffects(options => 
    options.AddEffectWorkflowBus(typeof(Program).Assembly)
);

// ✅ Solution: Register effect providers
services.AddChainSharpEffects(options => 
    options
        .AddPostgresEffect(connectionString)  // Add database persistence
        .SaveWorkflowParameters()              // Add parameter serialization
        .AddEffectWorkflowBus(typeof(Program).Assembly)
);
```

**Cause 2: SaveChanges not called**

This is typically handled automatically by EffectWorkflow, but if you're using custom implementations:

```csharp
// Ensure SaveChanges is called
await effectRunner.SaveChanges(cancellationToken);
```

## Performance Issues

### Problem: Slow workflow execution

#### Diagnostic Steps

1. **Check database queries**: Use SQL logging to identify slow queries
2. **Review step complexity**: Ensure steps are focused and efficient
3. **Check for N+1 queries**: Use eager loading where appropriate

#### Solutions

**Enable SQL logging:**

```csharp
services.AddDbContext<PostgresContext>(options =>
    options
        .UseNpgsql(connectionString)
        .EnableSensitiveDataLogging()
        .LogTo(Console.WriteLine, LogLevel.Information)
);
```

**Use projection for read operations:**

```csharp
// Instead of loading full entities
var metadata = await context.Metadatas
    .Where(m => m.WorkflowState == WorkflowState.Completed)
    .Select(m => new { m.Id, m.Name, m.EndTime })
    .ToListAsync();
```

### Problem: Memory leaks

#### Symptoms

- Increasing memory usage over time
- OutOfMemoryException after extended operation

#### Solutions

**Ensure proper disposal:**

```csharp
// Use 'using' statements or dispose manually
using var scope = serviceProvider.CreateScope();
var workflowBus = scope.ServiceProvider.GetRequiredService<IWorkflowBus>();
await workflowBus.RunAsync<MyOutput>(input);
// Scope and services are disposed here
```

**Check for large object retention:**

```csharp
// ❌ Problem: Holding references to large objects
public class MyWorkflow : EffectWorkflow<LargeInput, Output>
{
    private LargeInput _cachedInput; // This holds the reference
}

// ✅ Solution: Don't cache large objects
public class MyWorkflow : EffectWorkflow<LargeInput, Output>
{
    // Process and release
}
```

## Debugging Tips

### Enable detailed logging

```csharp
builder.Logging
    .AddConsole()
    .SetMinimumLevel(LogLevel.Debug)
    .AddFilter("ChainSharp", LogLevel.Trace);
```

### Use JSON Effect for debugging

```csharp
services.AddChainSharpEffects(options => 
    options
        .AddJsonEffect() // Logs all model state changes
        .AddEffectWorkflowBus(typeof(Program).Assembly)
);
```

### Inspect metadata in database

```sql
-- Find failed workflows
SELECT * FROM chain_sharp.metadata 
WHERE workflow_state = 'Failed'
ORDER BY start_time DESC;

-- Get workflow with error details
SELECT name, failure_exception, failure_reason, stack_trace 
FROM chain_sharp.metadata 
WHERE id = @workflowId;
```

## Getting Help

If you're still experiencing issues:

1. **Check the GitHub Issues**: Someone may have encountered the same problem
2. **Enable verbose logging**: Capture detailed logs before reporting
3. **Create a minimal reproduction**: Isolate the problem in a small example
4. **Open an issue**: Include logs, code samples, and environment details
