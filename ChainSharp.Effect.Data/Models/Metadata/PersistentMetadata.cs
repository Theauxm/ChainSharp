using System.ComponentModel.DataAnnotations.Schema;
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

            entity.HasIndex(e => e.ExternalId).IsUnique();

            entity
                .HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
