using ChainSharp.Effect.Models.Manifest;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataProcessing;

public record DataProcessingPipelineInput : IManifestProperties
{
    public string PipelineName { get; init; } = "DataProcessing";
}
