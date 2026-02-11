namespace ChainSharp.Effect.Orchestration.Scheduler.Workflows.ManifestExecutor;

/// <summary>
/// Request input for the ManifestExecutorWorkflow.
/// </summary>
/// <param name="MetadataId">The ID of the Metadata record to execute</param>
public record ExecuteManifestRequest(int MetadataId);
