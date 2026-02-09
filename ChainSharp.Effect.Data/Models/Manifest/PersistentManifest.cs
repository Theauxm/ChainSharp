using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Models.Manifest;

/// <summary>
/// Provides Entity Framework Core configuration for the Manifest model.
/// </summary>
/// <remarks>
/// This class inherits from the base Manifest model and provides the
/// OnModelCreating configuration needed for Entity Framework Core to
/// properly map the Manifest entity to the database.
///
/// The configuration includes:
/// 1. Table and schema mapping
/// 2. Primary key configuration with auto-generation
/// 3. JSONB column type for the Properties column
/// </remarks>
public class PersistentManifest : Effect.Models.Manifest.Manifest
{
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Effect.Models.Manifest.Manifest>(entity =>
        {
            entity.ToTable("manifest", "chain_sharp");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity
                .Property(e => e.Properties)
                .HasColumnType("jsonb");
        });
    }
}
