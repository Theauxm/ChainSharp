namespace ChainSharp.Effect.Models.Metadata.DTOs;

public class CreateMetadata
{
    public required string Name { get; set; }

    public required dynamic? Input { get; set; }

    public int? ParentId { get; set; }
}
