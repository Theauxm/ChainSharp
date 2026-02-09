using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChainSharp.Effect.Models.Manifest.DTOs;
using ChainSharp.Effect.Utils;
using LanguageExt;

namespace ChainSharp.Effect.Models.Manifest;

public class Manifest : IModel
{
  #region Columns

  [Column("id")] public int Id { get; }

  [Column("external_id")] public string ExternalId { get; set; }

  [Column("name")] public string Name { get; set; }

  [Column("property_type")] public string? PropertyTypeName { get; set; }

  [Column("properties")] public string? Properties { get; set; }

  [NotMapped]
  public Type PropertyType
    => PropertyTypeName == null
      ? typeof(Unit)
      : ResolveType(PropertyTypeName);

  [NotMapped]
  public Type NameType
    => Name == null
      ? typeof(Unit)
      : ResolveType(Name);

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
    };

    if (manifest.Properties != null)
      newManifest.SetProperties(manifest.Properties);

    return newManifest;
  }

  public Unit SetProperties(IManifestProperties properties)
  {
    var propertiesType = properties.GetType();

    PropertyTypeName = propertiesType.FullName;
    Properties = JsonSerializer.Serialize(properties, propertiesType, ChainSharpJsonSerializationOptions.Default);

    return Unit.Default;
  }

  public TProperty GetProperties<TProperty>()
    where TProperty : IManifestProperties
    => (TProperty)GetProperties(typeof(TProperty));

  public object GetProperties(Type propertyType)
  {
    if (propertyType != PropertyType)
      throw new Exception($"Passed type ({propertyType}) is not saved type ({PropertyType})");

    if (string.IsNullOrEmpty(Properties))
      throw new Exception($"Cannot deserialize null property object with type ({PropertyType})");

    var deserializedObject =
      JsonSerializer.Deserialize(Properties, propertyType, ChainSharpJsonSerializationOptions.Default) ??
      throw new Exception(
        $"Could not deserialize property object ({Properties}) with type ({PropertyType})");

    return deserializedObject;
  }

  #endregion

  [JsonConstructor]
  public Manifest()
  {
  }

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