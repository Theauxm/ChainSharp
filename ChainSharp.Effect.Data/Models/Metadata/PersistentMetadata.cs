using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Models.Metadata;

public class PersistentMetadata : Effect.Models.Metadata.Metadata
{
    #region ForeignKeys

    public PersistentMetadata Parent { get; private set; }

    public ICollection<PersistentMetadata> Children { get; private set; }

    #endregion

    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistentMetadata>(entity =>
        {
            // entity.ToTable("metadata", "chain_sharp");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.HasIndex(e => e.ExternalId).IsUnique();

            // entity.Property(e => e.ExternalId).HasColumnType("char(32)").IsRequired();
            // entity.Property(e => e.Name).HasColumnType("varchar").IsRequired();
            // entity.Property(e => e.StartTime).HasColumnType("timestamp with time zone").IsRequired();
            // entity.Property(e => e.EndTime).HasColumnType("timestamp with time zone");

            entity
                .HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // entity.Property(e => e.WorkflowState)
            // .HasColumnType("chain_sharp.workflow_state")
            // .HasDefaultValue("pending");
        });
    }
}
