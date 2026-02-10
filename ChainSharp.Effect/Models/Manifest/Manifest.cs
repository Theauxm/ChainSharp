using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Configuration.ChainSharpEffectConfiguration;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Utils;
using LanguageExt;

namespace ChainSharp.Effect.Models.Manifest;

/// <summary>
/// Represents a job definition that describes what workflow to run, how to schedule it,
/// and what retry policies to apply.
/// </summary>
/// <remarks>
/// A Manifest is the "job definition" in the scheduling system. It defines:
/// - Which workflow to execute (via <see cref="Name"/>)
/// - Default configuration/properties for the workflow
/// - Scheduling rules (cron, interval, or manual-only)
/// - Retry and timeout policies
///
/// Each execution of a Manifest creates a new <see cref="Metadata.Metadata"/> record.
/// This allows for full audit trail of every execution attempt.
/// </remarks>
public class Manifest : IModel
{
    #region Columns

    [Column("id")]
    public int Id { get; }

    [Column("external_id")]
    public string ExternalId { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("property_type")]
    public string? PropertyTypeName { get; set; }

    [Column("properties")]
    public string? Properties { get; set; }

    [NotMapped]
    public Type PropertyType =>
        PropertyTypeName == null ? typeof(Unit) : ResolveType(PropertyTypeName);

    [NotMapped]
    public Type NameType => Name == null ? typeof(Unit) : ResolveType(Name);

    #region Scheduling Properties

    /// <summary>
    /// Gets or sets whether this manifest is enabled for scheduling.
    /// </summary>
    /// <remarks>
    /// When false, the ManifestManager will skip this manifest during polling.
    /// This allows pausing jobs without deleting them.
    /// </remarks>
    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the scheduling strategy for this manifest.
    /// </summary>
    [Column("schedule_type")]
    public ScheduleType ScheduleType { get; set; } = ScheduleType.None;

    /// <summary>
    /// Gets or sets the cron expression for Cron-type schedules.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="ScheduleType"/> is <see cref="ScheduleType.Cron"/>.
    /// Uses standard cron format (e.g., "0 3 * * *" for daily at 3am).
    /// </remarks>
    [Column("cron_expression")]
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the interval in seconds for Interval-type schedules.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="ScheduleType"/> is <see cref="ScheduleType.Interval"/>.
    /// </remarks>
    [Column("interval_seconds")]
    public int? IntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts before dead-lettering.
    /// </summary>
    /// <remarks>
    /// Each retry creates a new Metadata record. After this many failed attempts,
    /// the job is moved to the dead letter queue for manual intervention.
    /// </remarks>
    [Column("max_retries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the timeout in seconds for job execution.
    /// </summary>
    /// <remarks>
    /// If a job is in "InProgress" state for longer than this duration,
    /// it may be considered stuck and subject to recovery logic.
    /// Null means use the global default from SchedulerConfiguration.
    /// </remarks>
    [Column("timeout_seconds")]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful execution.
    /// </summary>
    /// <remarks>
    /// Updated automatically when a job completes successfully.
    /// Useful for scheduling decisions and "delta mode" workflows that
    /// need to know when data was last synchronized.
    /// </remarks>
    [Column("last_successful_run")]
    public DateTime? LastSuccessfulRun { get; set; }

    #endregion

    #endregion

    #region ForeignKeys

    /// <summary>
    /// Gets the collection of metadata records (workflow executions) associated with this manifest.
    /// </summary>
    /// <remarks>
    /// This navigation property allows for traversal from a job definition (Manifest)
    /// to all its execution records (Metadata). It is populated by the ORM when loaded
    /// from the database.
    /// </remarks>
    public ICollection<Metadata.Metadata> Metadatas { get; private set; } = [];

    #endregion

    #region Functions

    public static Manifest Create(CreateManifest manifest)
    {
        if (manifest.Name.FullName is null)
            throw new Exception($"Could not get a full name from ({manifest.Name})");

        var newManifest = new Manifest()
        {
            Name = manifest.Name.FullName,
            ExternalId = Guid.NewGuid().ToString("N"),
            // Scheduling properties
            IsEnabled = manifest.IsEnabled,
            ScheduleType = manifest.ScheduleType,
            CronExpression = manifest.CronExpression,
            IntervalSeconds = manifest.IntervalSeconds,
            MaxRetries = manifest.MaxRetries,
            TimeoutSeconds = manifest.TimeoutSeconds,
        };

        if (manifest.Properties != null)
            newManifest.SetProperties(manifest.Properties);

        return newManifest;
    }

    public Unit SetProperties(IManifestProperties properties)
    {
        var propertiesType = properties.GetType();

        PropertyTypeName = propertiesType.FullName;
        Properties = JsonSerializer.Serialize(
            properties,
            propertiesType,
            ChainSharpJsonSerializationOptions.Default
        );

        return Unit.Default;
    }

    public TProperty GetProperties<TProperty>()
        where TProperty : IManifestProperties => (TProperty)GetProperties(typeof(TProperty));

    public object GetProperties(Type propertyType)
    {
        if (propertyType != PropertyType)
            throw new Exception($"Passed type ({propertyType}) is not saved type ({PropertyType})");

        if (string.IsNullOrEmpty(Properties))
            throw new Exception(
                $"Cannot deserialize null property object with type ({PropertyType})"
            );

        var deserializedObject =
            JsonSerializer.Deserialize(
                Properties,
                propertyType,
                ChainSharpJsonSerializationOptions.Default
            )
            ?? throw new Exception(
                $"Could not deserialize property object ({Properties}) with type ({PropertyType})"
            );

        return deserializedObject;
    }

    public override string ToString() =>
        JsonSerializer.Serialize(
            this,
            GetType(),
            ChainSharpEffectConfiguration.StaticSystemJsonSerializerOptions
        );

    #endregion

    [JsonConstructor]
    public Manifest() { }

    /// <summary>
    /// Resolves a type by its full name, searching all loaded assemblies.
    /// </summary>
    private static Type ResolveType(string typeName)
    {
        // First try the standard Type.GetType which works for types in the current assembly and mscorlib
        var type = Type.GetType(typeName);
        if (type != null)
            return type;

        // Search through all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        throw new TypeLoadException($"Unable to find type: {typeName}");
    }
}
