using System.ComponentModel.DataAnnotations.Schema;

namespace ChainSharp.Effect.Models;

/// <summary>
/// Defines the contract for all persistable models within the ChainSharp.Effect system.
/// This interface serves as the base for all entities that can be tracked and stored
/// by effect providers.
/// </summary>
/// <remarks>
/// The IModel interface is a fundamental abstraction in the ChainSharp.Effect system.
/// It represents any entity that can be persisted to a storage mechanism, such as
/// a database, file, or other persistent store.
/// 
/// All models in the system implement this interface, which ensures they have:
/// 1. A unique identifier (Id)
/// 2. Consistent column naming conventions through attributes
/// 3. A common type that can be used by generic tracking methods
/// 
/// This interface is used by the IEffectProvider.Track method to accept any
/// type of model for tracking and persistence.
/// </remarks>
public interface IModel
{
    /// <summary>
    /// Gets the unique identifier for this model.
    /// </summary>
    /// <remarks>
    /// The Id property is the primary key for the model in the database.
    /// It is used to uniquely identify each instance of a model and to
    /// establish relationships between models.
    /// 
    /// The Column attribute specifies the name of the column in the database
    /// that corresponds to this property. Using lowercase column names is
    /// a convention in the ChainSharp.Effect system to ensure compatibility
    /// with different database systems.
    /// </remarks>
    [Column("id")]
    int Id { get; }
}
