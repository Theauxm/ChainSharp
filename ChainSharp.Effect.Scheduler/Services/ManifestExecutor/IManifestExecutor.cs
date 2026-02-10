namespace ChainSharp.Effect.Scheduler.Services.ManifestExecutor;

/// <summary>
/// Executes workflow jobs that have been scheduled via the manifest system.
/// </summary>
public interface IManifestExecutor
{
  /// <summary>
  /// Executes a scheduled job by its Metadata ID.
  /// </summary>
  /// <param name="metadataId">The Metadata record containing job details</param>
  /// <param name="cancellationToken">Cancellation token for the operation</param>
  Task ExecuteAsync(int metadataId, CancellationToken cancellationToken = default);
}
