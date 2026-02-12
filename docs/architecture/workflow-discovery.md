---
layout: default
title: Workflow Discovery
parent: Architecture
nav_order: 3
---

# Workflow Discovery & Routing

## WorkflowRegistry

```csharp
public class WorkflowRegistry : IWorkflowRegistry
{
    public Dictionary<Type, Type> InputTypeToWorkflow { get; set; }

    public WorkflowRegistry(params Assembly[] assemblies)
    {
        // Scan assemblies for workflow implementations
        var workflowType = typeof(IEffectWorkflow<,>);
        var allWorkflowTypes = ScanAssembliesForWorkflows(assemblies, workflowType);

        // Create mapping: InputType -> WorkflowType
        InputTypeToWorkflow = allWorkflowTypes.ToDictionary(
            workflowType => ExtractInputType(workflowType),
            workflowType => workflowType
        );
    }
}
```

## WorkflowBus

```csharp
public class WorkflowBus : IWorkflowBus
{
    public async Task<TOut> RunAsync<TOut>(object workflowInput, Metadata? parentMetadata = null)
    {
        // 1. Find workflow type by input type
        var inputType = workflowInput.GetType();
        var workflowType = _registryService.InputTypeToWorkflow[inputType];

        // 2. Resolve workflow from DI container
        var workflow = _serviceProvider.GetRequiredService(workflowType);

        // 3. Inject internal workflow properties (framework-level only)
        _serviceProvider.InjectProperties(workflow);

        // 4. Set up parent-child relationship if needed
        if (parentMetadata != null)
            SetParentId(workflow, parentMetadata.Id);

        // 5. Execute workflow using reflection
        return await InvokeWorkflowRun<TOut>(workflow, workflowInput);
    }
}
```

## Key Constraints and Design Decisions

### Input Type Uniqueness Constraint

**Critical:** Each input type can only map to ONE workflow.

```csharp
// ❌ This will cause conflicts
public class CreateUserWorkflow : EffectWorkflow<UserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UserRequest, User> { }

// ✅ This works correctly
public class CreateUserWorkflow : EffectWorkflow<CreateUserRequest, User> { }
public class UpdateUserWorkflow : EffectWorkflow<UpdateUserRequest, User> { }
```

### Workflow Discovery Rules

1. **Must be concrete classes** (not abstract)
2. **Must implement IEffectWorkflow<,>**
3. **Must have parameterless constructor or be registered in DI**
4. **Should implement a non-generic interface** for better DI integration
