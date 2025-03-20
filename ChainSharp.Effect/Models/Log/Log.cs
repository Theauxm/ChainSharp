using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Models.Log.DTOs;
using Microsoft.Extensions.Logging;

namespace ChainSharp.Effect.Models.Log;

public class Log : ILog
{
    #region Columns

    [Column("id")]
    [JsonPropertyName("id")]
    public int Id { get; private set; }

    [Column("metadata_id")]
    [JsonPropertyName("metadata_id")]
    [JsonInclude]
    public int MetadataId { get; private set; }

    [Column("event_id")]
    [JsonPropertyName("event_id")]
    public int EventId { get; set; }

    [Column("level")]
    [JsonPropertyName("level")]
    public LogLevel Level { get; set; }

    [Column("message")]
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [Column("category")]
    [JsonPropertyName("category")]
    public string Category { get; set; }

    [Column("exception")]
    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [Column("stack_trace")]
    [JsonPropertyName("stack_trace")]
    public string? StackTrace { get; set; }

    #endregion

    #region ForeignKeys

    public Metadata.Metadata Metadata { get; set; }

    #endregion

    #region Functions

    public static Log Create(CreateLog createLog)
    {
        var newLog = new Log()
        {
            Level = createLog.Level,
            Message = createLog.Message,
            Category = createLog.CategoryName,
            EventId = createLog.EventId,
            Exception = createLog.Exception?.Message,
            StackTrace = createLog.Exception?.StackTrace
        };

        return newLog;
    }

    #endregion

    [JsonConstructor]
    public Log() { }
}
