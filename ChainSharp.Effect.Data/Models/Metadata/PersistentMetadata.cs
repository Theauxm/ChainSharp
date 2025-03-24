using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Models.Metadata;

public class PersistentMetadata : Effect.Models.Metadata.Metadata
{
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Effect.Models.Metadata.Metadata>(entity =>
        {
            entity.ToTable("metadata", "chain_sharp");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Ignore(e => e.InputObject);
            entity.Ignore(e => e.OutputObject);

            entity.HasIndex(e => e.ExternalId).IsUnique();

            entity
                .HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // // Configure the conversion from string to JsonDocument and vice versa
            // entity
            //     .Property(e => e.Input)
            //     .HasConversion(
            //         v => v != null ? v.ToString() : null,
            //         v => v != null ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null
            //     );
            //
            // entity
            //     .Property(e => e.Output)
            //     .HasConversion(
            //         v => v != null ? v.ToString() : null,
            //         v => v != null ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null
            //     );
        });
    }
}
