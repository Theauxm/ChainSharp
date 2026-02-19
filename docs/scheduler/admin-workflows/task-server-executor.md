---
layout: default
title: TaskServerExecutor
parent: Administrative Workflows
grand_parent: Scheduling
nav_order: 3
---

# TaskServerExecutorWorkflow

The TaskServerExecutor is what actually runs your workflow. It executes on background task server workers (Hangfire, typically) and handles the bookkeeping around each execution: loading the metadata, validating state, invoking the workflow, and recording success.

## Chain

```
LoadMetadata → ValidateMetadataState → ExecuteScheduledWorkflow →
                                        UpdateManifestSuccess → SaveDatabaseChanges
```

## Input

```csharp
public record ExecuteManifestRequest(int MetadataId, object? Input);
```

The `MetadataId` points to the `Metadata` row created by the [JobDispatcher](job-dispatcher.md). The `Input` is the deserialized workflow input passed through from the work queue.

## Steps

### LoadMetadataStep

Loads the `Metadata` record by ID, eagerly including its `Manifest` navigation (needed later by `UpdateManifestSuccessStep`). If the input includes a non-null `Input` object, it wraps it in a `ResolvedWorkflowInput` for type-safe routing through ChainSharp's memory system.

### ValidateMetadataStateStep

Checks that the loaded metadata is in `WorkflowState.Pending`. If it's already `InProgress`, `Completed`, or `Failed`, the step throws. This guards against duplicate execution—if Hangfire retries a job that already started, this step catches it.

### ExecuteScheduledWorkflowStep

Resolves the target workflow via `IWorkflowBus` using the deserialized input and invokes it. This is where your workflow's `RunInternal` method gets called. The workflow runs as a nested workflow under the TaskServerExecutor's own metadata, maintaining the parent-child relationship in the metadata tree.

### UpdateManifestSuccessStep

If the workflow completed successfully and the metadata has an associated manifest, updates `Manifest.LastSuccessfulRun` to `DateTime.UtcNow`. This timestamp is what drives [dependent workflow](../dependent-workflows.md) evaluation—downstream manifests won't fire until this value advances past their own `LastSuccessfulRun`.

If there's no manifest (e.g., an ad-hoc execution), this step is a no-op.

### SaveDatabaseChangesStep

Persists all pending database changes—primarily the `LastSuccessfulRun` update. This is a separate step rather than being folded into `UpdateManifestSuccessStep` so that the save happens as its own observable step in the chain, with its own timing in step metadata.

## Assembly Registration

The `TaskServerExecutorWorkflow` lives in the `ChainSharp.Effect.Orchestration.Scheduler` assembly. The `WorkflowBus` discovers workflows by scanning assemblies, so this assembly must be registered:

```csharp
builder.Services.AddChainSharpEffects(options => options
    .AddEffectWorkflowBus(
        typeof(Program).Assembly,
        typeof(TaskServerExecutorWorkflow).Assembly  // required
    )
);
```

*API Reference: [AddEffectWorkflowBus]({{ site.baseurl }}{% link api-reference/configuration/add-effect-workflow-bus.md %})*

If you forget this, scheduled jobs will silently fail—Hangfire will invoke the TaskServerExecutor, but the WorkflowBus won't find it. No error, just nothing happens.
