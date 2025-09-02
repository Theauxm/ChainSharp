using LanguageExt;

namespace ChainSharp.Effect.Models.StepMetadata.DTOs;

public class CreateStepMetadata
{
    public string Name { get; set; }
    public string ExternalId { get; set; }
    public string WorkflowExternalId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public Type InputType { get; set; }
    public Type OutputType { get; set; }
    public EitherStatus State { get; set; }
}
