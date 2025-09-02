using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.StepMetadata.DTOs;
using LanguageExt;

namespace ChainSharp.Effect.Models.StepMetadata;

public class StepMetadata : IStepMetadata
{
    #region Columns

    [Column("id")]
    [JsonPropertyName("id")]
    public int Id { get; private set; }

    [Column("name")]
    [JsonPropertyName("name")]
    public string Name { get; private set; }

    [Column("external_id")]
    [JsonPropertyName("external_id")]
    public string ExternalId { get; private set; }

    [Column("workflow_external_id")]
    [JsonPropertyName("workflow_external_id")]
    public string WorkflowExternalId { get; private set; }

    [Column("start_time_utc")]
    [JsonPropertyName("start_time_utc")]
    public DateTime StartTimeUtc { get; set; }

    [Column("end_time_utc")]
    [JsonPropertyName("end_time_utc")]
    public DateTime EndTimeUtc { get; set; }

    [Column("input_type")]
    [JsonPropertyName("input_type")]
    public Type InputType { get; private set; }

    [Column("output_type")]
    [JsonPropertyName("output_type")]
    public Type OutputType { get; private set; }

    [Column("state")]
    [JsonPropertyName("state")]
    public EitherStatus State { get; set; }

    #endregion

    #region ForeignKeys

    #endregion

    #region Functions

    public static StepMetadata Create(CreateStepMetadata stepMetadata)
    {
        var newStepMetadata = new StepMetadata()
        {
            Name = stepMetadata.Name,
            ExternalId = stepMetadata.ExternalId,
            WorkflowExternalId = stepMetadata.WorkflowExternalId,
            StartTimeUtc = stepMetadata.StartTimeUtc,
            EndTimeUtc = stepMetadata.EndTimeUtc,
            InputType = stepMetadata.InputType,
            OutputType = stepMetadata.OutputType,
            State = stepMetadata.State,
        };

        return newStepMetadata;
    }

    #endregion

    [JsonConstructor]
    public StepMetadata() { }
}
