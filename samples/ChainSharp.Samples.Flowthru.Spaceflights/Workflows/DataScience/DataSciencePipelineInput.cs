using ChainSharp.Effect.Models.Manifest;

namespace ChainSharp.Samples.Flowthru.Spaceflights.Workflows.DataScience;

public record DataSciencePipelineInput : IManifestProperties
{
    public string PipelineName { get; init; } = "DataScience";
}
