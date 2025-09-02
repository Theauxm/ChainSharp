using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Models.StepMetadata.DTOs;
using LanguageExt;

namespace ChainSharp.Effect.Models.StepMetadata;

public class StepMetadata : IStepMetadata
{
    #region Columns

    [Column("id")]
    public int Id { get; private set; }

    [Column("workflow_name")]
    public string WorkflowName { get; private set; }

    [Column("name")]
    public string Name { get; private set; }

    [Column("external_id")]
    public string ExternalId { get; private set; }

    [Column("workflow_external_id")]
    public string WorkflowExternalId { get; private set; }

    [Column("start_time_utc")]
    public DateTime? StartTimeUtc { get; set; }

    [Column("end_time_utc")]
    public DateTime? EndTimeUtc { get; set; }

    [Column("input_type")]
    public Type InputType { get; private set; }

    [Column("output_type")]
    public Type OutputType { get; private set; }

    [Column("state")]
    public EitherStatus State { get; set; }

    [Column("has_ran")]
    public bool HasRan { get; set; }

    [Column("output_json")]
    public string? OutputJson { get; set; }

    #endregion

    #region ForeignKeys

    #endregion

    #region Functions

    public static StepMetadata Create(CreateStepMetadata stepMetadata, Metadata.Metadata metadata)
    {
        var newStepMetadata = new StepMetadata
        {
            Name = stepMetadata.Name,
            ExternalId = stepMetadata.ExternalId,
            WorkflowExternalId = metadata.ExternalId,
            WorkflowName = metadata.Name,
            StartTimeUtc = stepMetadata.StartTimeUtc,
            EndTimeUtc = stepMetadata.EndTimeUtc,
            InputType = stepMetadata.InputType,
            OutputType = stepMetadata.OutputType,
            State = stepMetadata.State,
            HasRan = false
        };

        return newStepMetadata;
    }

    public override string ToString() =>
        JsonSerializer.Serialize(
            this,
            ChainSharpEffectConfiguration.StaticSystemJsonSerializerOptions
        );

    #endregion

    [JsonConstructor]
    public StepMetadata() { }
}
