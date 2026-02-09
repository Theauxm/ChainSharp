using System.ComponentModel.DataAnnotations.Schema;

namespace ChainSharp.Effect.Models.Manifest;

public interface IManifest : IModel
{
  [Column("external_id")] string ExternalId { get; set; }

  [Column("name")] string Name { get; set; }
  [Column("fullname")] string FullName { get; set; }
  [Column("property_type")] string PropertyType { get; set; }
  [Column("properties")] string Properties { get; set; }
}