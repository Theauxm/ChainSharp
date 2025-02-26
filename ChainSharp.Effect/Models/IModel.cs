using System.ComponentModel.DataAnnotations.Schema;

namespace ChainSharp.Effect.Models;

/// <summary>
/// Represents all models within the chain_sharp database
/// </summary>
public interface IModel
{
    [Column("id")]
    public int Id { get; }
}
