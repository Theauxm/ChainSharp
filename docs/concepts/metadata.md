---
layout: default
title: Metadata
parent: Core Concepts
nav_order: 4
---

# Metadata

Every workflow execution creates a metadata record:

```csharp
public class Metadata : IModel, IDisposable
{
    public int Id { get; }                          // Unique identifier
    public string Name { get; set; }                // Workflow name
    public string ExternalId { get; set; }          // GUID for external references
    public string? Executor { get; }                // Assembly that ran the workflow
    public WorkflowState WorkflowState { get; set; } // Pending/InProgress/Completed/Failed
    public DateTime StartTime { get; set; }         // When workflow started
    public DateTime? EndTime { get; set; }          // When workflow finished
    public string? Input { get; set; }              // Serialized input (jsonb)
    public string? Output { get; set; }             // Serialized output (jsonb)
    public string? FailureStep { get; }             // Which step failed
    public string? FailureException { get; }        // Exception type
    public string? FailureReason { get; }           // Error message
    public string? StackTrace { get; set; }         // Stack trace if failed
    public int? ParentId { get; set; }              // For nested workflows
    public int? ManifestId { get; set; }            // For scheduled workflows
}
```

The `WorkflowState` tracks progress: `Pending` → `InProgress` → `Completed` (or `Failed`). If a workflow fails, the metadata captures the exception, stack trace, and which step failed.

## Nested Workflows

Workflows can run other workflows by injecting `IWorkflowBus`. Pass the current `Metadata` to the child workflow to establish a parent-child relationship—this creates a tree of metadata records you can query to trace execution across workflows.

See [Nested Workflows](../mediator.md#nested-workflows) for implementation details.

## Execution Flow (EffectWorkflow)

```
[Client Request]
       │
       ▼
[WorkflowBus.RunAsync]
       │
       ▼
[Find Workflow by Input Type]
       │
       ▼
[Create Workflow Instance]
       │
       ▼
[Inject Dependencies]
       │
       ▼
[Initialize Metadata]
       │
       ▼
[Execute Workflow Chain]
       │
       ▼
   Success? ──No──► [Update Metadata: Failed]
       │                      │
      Yes                     │
       │                      │
       ▼                      ▼
[Update Metadata: Completed]  │
       │                      │
       └──────────┬───────────┘
                  │
                  ▼
       [SaveChanges - Execute Effects]
                  │
                  ▼
           [Return Result]
```
