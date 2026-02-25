using ChainSharp.Effect.Models.Manifest;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.Reporting;

public record ReportingPipelineInput : IManifestProperties
{
    public string PipelineName { get; init; } = "Reporting";
}
