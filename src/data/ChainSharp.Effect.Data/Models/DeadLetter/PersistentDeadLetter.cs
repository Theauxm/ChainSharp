using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Models.DeadLetter;

public class PersistentDeadLetter : Effect.Models.DeadLetter.DeadLetter
{
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Effect.Models.DeadLetter.DeadLetter>(entity =>
        {
            entity.ToTable("dead_letter", "chain_sharp");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity
                .HasOne(x => x.Manifest)
                .WithMany(m => m.DeadLetters)
                .HasForeignKey(x => x.ManifestId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
