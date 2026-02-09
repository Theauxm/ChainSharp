namespace ChainSharp.Effect.Models.Manifest.DTOs;

public class CreateManifest
{
  public required Type Name { get; set; }

  public Type? PropertyType { get; set; }

  public dynamic? Properties { get; set; }
}