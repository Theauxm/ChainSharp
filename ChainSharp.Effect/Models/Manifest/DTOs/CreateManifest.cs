namespace ChainSharp.Effect.Models.Manifest.DTOs;

public class CreateManifest
{
    public required Type Name { get; set; }

    public IManifestProperties? Properties { get; set; }
}
