using System.ComponentModel.DataAnnotations.Schema;
using ChainSharp.Effect.Enums;
using LanguageExt;

namespace ChainSharp.Effect.Models.StepMetadata;

public interface IStepMetadata : IModel
{
    [Column("name")]
    string Name { get; }

    [Column("external_id")]
    string ExternalId { get; }

    [Column("workflow_external_id")]
    string WorkflowExternalId { get; }

    [Column("start_time_utc")]
    DateTime StartTimeUtc { get; }

    [Column("end_time_utc")]
    DateTime EndTimeUtc { get; }

    [Column("input_type")]
    Type InputType { get; }

    [Column("output_type")]
    Type OutputType { get; }

    [Column("state")]
    EitherStatus State { get; }
}
