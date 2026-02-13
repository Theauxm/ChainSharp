namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.TaskServerExecutor;

/// <summary>
/// Request input for the TaskServerExecutorWorkflow.
/// </summary>
/// <param name="MetadataId">The ID of the Metadata record to execute</param>
/// <param name="Input">
/// Optional workflow input object for ad-hoc executions (e.g., from the dashboard).
/// When null, the input is resolved from the associated Manifest's properties.
/// </param>
public record ExecuteManifestRequest(int MetadataId, object? Input = null);
