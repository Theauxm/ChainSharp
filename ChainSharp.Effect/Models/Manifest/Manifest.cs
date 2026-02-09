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

  [Column("external_id")] string ExternalId { get; set; }

  [Column("name")] string Name { get; set; }

  [Column("property_type")] string? PropertyTypeName { get; set; }

  [Column("properties")] private string? Properties { get; set; }

  [NotMapped]
  public Type SerializerType
    => PropertyTypeName is null
      ? typeof(Unit)
      : Type.GetType(PropertyTypeName)!;

  [NotMapped]
  public Type NameType
    => Name is null
      ? typeof(Unit)
      : Type.GetType(Name)!;

  #endregion

  #region Functions

  public static Manifest Create(CreateManifest manifest)
  {
    if (manifest.NameType.FullName is null)
      throw new Exception($"Could not get a full name from ({manifest.NameType})");

    var newManifest = new Manifest()
    {
      Name = manifest.NameType.FullName,
      ExternalId = Guid.NewGuid().ToString("N"),
    };

    if (manifest.PropertyType != null && manifest.Properties != null)
      newManifest.SetProperties(manifest.PropertyType, manifest.Properties);

    return newManifest;
  }

  public Unit SetProperties<TProperty>(TProperty config)
    => SetProperties((typeof(TProperty), config));

  public Unit SetProperties(Type configType, dynamic config)
  {
    if (config.GetType() != configType)
      throw new Exception(
        $"Actual Config Type ({config.GetType().FullName}) does not match passed Passed Config Type ({configType.FullName})");

    PropertyTypeName = configType.FullName;
    Properties = JsonSerializer.Serialize(config);

    return Unit.Default;
  }

  public TProperty GetProperties<TProperty>()
    => (TProperty)GetProperties(typeof(TProperty));

  public object GetProperties(Type propertyType)
  {
    if (propertyType != SerializerType)
      throw new Exception($"{propertyType.FullName} is not ({SerializerType.FullName})");

    if (string.IsNullOrEmpty(Properties))
      throw new Exception($"Cannot deserialize null property object with type ({SerializerType.FullName})");

    var deserializedObject =
      JsonSerializer.Deserialize(Properties, propertyType, ChainSharpJsonSerializationOptions.Default) ??
      throw new Exception(
        $"Could not deserialize property object ({Properties}) with type ({SerializerType.FullName})");

    return deserializedObject;
  }

  #endregion

  [JsonConstructor]
  public Manifest()
  {
  }
}